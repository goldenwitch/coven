// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Coven.Spellcasting.Agents;

namespace Coven.Toys.MockProcess;

/// <summary>
/// Simple ITailMux implementation that lets callers provide delegates for
/// write handling and optionally feed output events. Intended for demos/testing.
/// </summary>
internal sealed class LambdaTailMux : ITailMux
{
    private readonly Func<string, CancellationToken, Task> _onWrite;
    private readonly Channel<TailEvent> _chan;
    private readonly ILogger<LambdaTailMux> _log;
    private volatile bool _disposed;

    public LambdaTailMux(Func<string, CancellationToken, Task> onWrite, int capacity = 256, ILogger<LambdaTailMux>? log = null)
    {
        _onWrite = onWrite ?? throw new ArgumentNullException(nameof(onWrite));
        _chan = Channel.CreateBounded<TailEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _log = log ?? NullLogger<LambdaTailMux>.Instance;
    }

    public async Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default)
    {
        if (onMessage is null) throw new ArgumentNullException(nameof(onMessage));
        ThrowIfDisposed();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var ev = await _chan.Reader.ReadAsync(ct).AsTask().ConfigureAwait(false);
                await onMessage(ev).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }
    }

    public Task WriteAsync(string data, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _onWrite(data, ct);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        try { _chan.Writer.TryComplete(); } catch { }
        return ValueTask.CompletedTask;
    }

    public Task FeedAsync(TailEvent ev, CancellationToken ct = default)
        => _chan.Writer.WriteAsync(ev, ct).AsTask();

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LambdaTailMux));
    }
}
