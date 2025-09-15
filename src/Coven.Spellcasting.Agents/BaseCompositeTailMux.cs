// SPDX-License-Identifier: BUSL-1.1

using System;

namespace Coven.Spellcasting.Agents;

/// <summary>
/// Base implementation that composes a send port and a tail source,
/// provides single-reader semantics, forwarding, and coordinated disposal.
/// </summary>
public abstract class BaseCompositeTailMux<TSend, TTail> : ITailMux
    where TSend : ISendPort
    where TTail : ITailSource
{
    private readonly CancellationTokenSource _cts = new();
    private int _activeTails;
    private bool _disposed;

    protected BaseCompositeTailMux(TSend sendPort, TTail tailSource)
    {
        SendPort = sendPort;
        TailSource = tailSource;
    }

    public TSend SendPort { get; }
    public TTail TailSource { get; }

    public async Task TailAsync(Func<TailEvent, ValueTask> onMessage, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (Interlocked.Increment(ref _activeTails) != 1)
        {
            Interlocked.Decrement(ref _activeTails);
            throw new InvalidOperationException("Only one TailAsync reader is supported at a time.");
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            await TailSource.TailAsync(onMessage, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _activeTails);
        }
    }

    public Task WriteAsync(string data, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return SendPort.WriteAsync(data, ct);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { await TailSource.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await SendPort.DisposeAsync().ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }
}
