// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;
using Coven.Core;
using Coven.Core.Daemonology;
using Microsoft.Extensions.Logging;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Tails a FileScrivener and flushes snapshots to a sink when a predicate is met.
/// Implements a producer (tail→snapshot→enqueue) and consumer (dequeue→append) pair with buffer reuse (swap mechanics).
/// </summary>
internal sealed class FlusherDaemon<TEntry>(
    IScrivener<TEntry> scrivener,
    IFlushSink<TEntry> sink,
    IFlushPredicate<TEntry> predicate,
    FileScrivenerConfig config,
    ILogger<FlusherDaemon<TEntry>> logger,
    IScrivener<DaemonEvent> daemonJournal)
    : ContractDaemon(daemonJournal), IAsyncDisposable where TEntry : notnull
{
    private readonly IScrivener<TEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly IFlushSink<TEntry> _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    private readonly IFlushPredicate<TEntry> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly FileScrivenerConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger<FlusherDaemon<TEntry>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Producer-owned active buffer. Only the producer task appends to the list.
    // Reference swaps are done atomically to avoid duplication during shutdown races.
    private List<(long position, TEntry entry)> _activeSnapshot = [];

    private Channel<List<(long position, TEntry entry)>> _flushQueue = Channel.CreateUnbounded<List<(long position, TEntry entry)>>();
    private Channel<List<(long position, TEntry entry)>> _pool = Channel.CreateUnbounded<List<(long position, TEntry entry)>>();
    private CancellationTokenSource? _cts;
    private Task? _producer;
    private Task? _consumer;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        // Flush queue (bounded)
        _flushQueue = Channel.CreateBounded<List<(long position, TEntry entry)>>(new BoundedChannelOptions(_config.FlushQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Buffer pool (bounded). Slightly larger than flush queue to always have an active buffer.
        int poolCapacity = _config.FlushQueueCapacity + 2;
        _pool = Channel.CreateBounded<List<(long position, TEntry entry)>>(new BoundedChannelOptions(poolCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        // Pre-fill the pool to avoid allocations in steady state. Capacity is tuned for amortized growth.
        for (int i = 0; i < poolCapacity; i++)
        {
            _pool.Writer.TryWrite(new List<(long position, TEntry entry)>(capacity: 128));
        }

        // Rent initial active buffer
        if (!_pool.Reader.TryRead(out _activeSnapshot!))
        {
            _activeSnapshot = new List<(long position, TEntry entry)>(capacity: 128);
        }

        _producer = Task.Run(() => ProducerAsync(ct), ct);
        _consumer = Task.Run(() => ConsumerAsync(ct), ct);

        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        try
        {
            if (_producer is not null && _consumer is not null)
            {
                try
                {
                    await Task.WhenAll(_producer, _consumer).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // cooperative shutdown
                }
            }
        }
        finally
        {
            await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<(long position, TEntry entry)>> RentBufferAsync(CancellationToken ct)
    {
        return _pool.Reader.TryRead(out List<(long position, TEntry entry)>? buf)
            ? buf
            : await _pool.Reader.ReadAsync(ct).ConfigureAwait(false);
    }

    private async Task ProducerAsync(CancellationToken ct)
    {
        try
        {
            await foreach ((long pos, TEntry entry) in _scrivener.TailAsync(0, ct))
            {
                // Append to current snapshot.
                // Expected state: _activeSnapshot is exclusively owned by producer, safe to append without locking.
                _activeSnapshot.Add((pos, entry));

                if (_predicate.ShouldFlush(_activeSnapshot))
                {
                    // Rent a fresh buffer, then atomically swap references to avoid any window
                    // where shutdown could observe the pre-swap active list.
                    List<(long position, TEntry entry)> fresh = await RentBufferAsync(ct).ConfigureAwait(false);
                    fresh.Clear();

                    // Re-check after allocation to avoid swapping on stale predicate truth.
                    if (_predicate.ShouldFlush(_activeSnapshot))
                    {
                        List<(long position, TEntry entry)> toFlush = Interlocked.Exchange(ref _activeSnapshot, fresh);
                        FlusherLog.SnapshotFlushTriggered(_logger, toFlush.Count);

                        await _flushQueue.Writer.WriteAsync(toFlush, ct).ConfigureAwait(false);
                        FlusherLog.SnapshotEnqueued(_logger, toFlush.Count);
                    }
                    else
                    {
                        // Predicate flipped; return the unused fresh buffer to the pool.
                        await _pool.Writer.WriteAsync(fresh, ct).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            FlusherLog.ProducerCanceled(_logger);
        }
        catch (Exception ex)
        {
            FlusherLog.ProducerFailed(_logger, ex);
            await Fail(ex, ct).ConfigureAwait(false);
        }
        finally
        {
            // On completion/cancel, push any remaining snapshot. Atomically detach the active buffer
            // to avoid any chance of double-enqueue with an in-flight swap.
            List<(long position, TEntry entry)> remainder = Interlocked.Exchange(ref _activeSnapshot, new List<(long position, TEntry entry)>(capacity: 128));
            if (remainder.Count > 0)
            {
                try
                {
                    _flushQueue.Writer.TryWrite(remainder);
                    FlusherLog.ShutdownRemainderEnqueued(_logger, remainder.Count);
                }
                catch
                {
                    // ignore on shutdown
                }
            }
            _flushQueue.Writer.TryComplete();
        }
    }

    private async Task ConsumerAsync(CancellationToken ct)
    {
        try
        {
            ChannelReader<List<(long position, TEntry entry)>> reader = _flushQueue.Reader;
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out List<(long position, TEntry entry)>? batch))
                {
                    // Persist ordered batch to sink.
                    await _sink.AppendSnapshotAsync(batch, ct).ConfigureAwait(false);
                    FlusherLog.SnapshotAppended(_logger, batch.Count);
                    // Return buffer to pool for reuse by producer.
                    batch.Clear();
                    await _pool.Writer.WriteAsync(batch, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            FlusherLog.ConsumerCanceled(_logger);
        }
        catch (Exception ex)
        {
            FlusherLog.ConsumerFailed(_logger, ex);
            await Fail(ex, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Status != Status.Completed)
            {
                await Shutdown(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _cts?.Dispose();
            _producer = null;
            _consumer = null;
            GC.SuppressFinalize(this);
        }
        return;
    }
}
