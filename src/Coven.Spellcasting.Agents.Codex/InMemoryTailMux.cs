using System.Threading.Channels;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// In-memory ITailMux implementation.
/// - TailAsync consumes TailEvents from an internal channel.
/// - WriteLineAsync posts lines to an outgoing sink (in-memory), not to Tail events.
/// - Tests (or callers) can feed incoming events via <see cref="FeedAsync"/>.
/// This models the asymmetric read/write pattern in-memory.
/// </summary>
internal sealed class InMemoryTailMux : ITailMux
{
    // Incoming events for TailAsync
    private readonly Channel<TailEvent> _incoming =
        Channel.CreateBounded<TailEvent>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    // Outgoing sink for posted writes (useful for validation in tests)
    private readonly Channel<string> _writes =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _disposed;
    private int _activeTails;

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
            var token = linked.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var item = await _incoming.Reader.ReadAsync(token).AsTask().ConfigureAwait(false);
                    await onMessage(item).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeTails);
        }
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writes.Writer.WriteAsync(line, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _incoming.Writer.TryComplete(); } catch { }
        try { _writes.Writer.TryComplete(); } catch { }

        _writeLock.Dispose();
        _cts.Dispose();

        // Drain any pending tasks by yielding
        await Task.Yield();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryTailMux));
    }

    // ---- Internal hooks for tests/consumers ----
    internal ValueTask FeedAsync(TailEvent ev, CancellationToken ct = default)
        => _incoming.Writer.WriteAsync(ev, ct);

    internal IAsyncEnumerable<string> ReadWritesAsync(CancellationToken ct = default)
    {
        return ReadAllAsync(_writes.Reader, ct);
    }

    private static async IAsyncEnumerable<string> ReadAllAsync(ChannelReader<string> reader, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            bool canRead;
            try
            {
                canRead = await reader.WaitToReadAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (ChannelClosedException)
            {
                yield break;
            }

            if (!canRead)
                yield break;

            while (reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }
}
