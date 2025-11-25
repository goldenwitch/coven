// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;
using Coven.Core;
using Coven.Daemonology;
using Microsoft.Extensions.Logging;

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Tails a FileScrivener and flushes snapshots to a sink when a predicate is met.
/// Implements a producer (tail→snapshot→enqueue) and consumer (dequeue→append) pair.
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

    private readonly Lock _lock = new();
    private readonly List<(long position, TEntry entry)> _snapshot = [];

    private Channel<IReadOnlyList<(long position, TEntry entry)>> _flushQueue = Channel.CreateUnbounded<IReadOnlyList<(long position, TEntry entry)>>();
    private CancellationTokenSource? _cts;
    private Task? _producer;
    private Task? _consumer;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        _flushQueue = Channel.CreateBounded<IReadOnlyList<(long position, TEntry entry)>>(new BoundedChannelOptions(_config.FlushQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

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

    private async Task ProducerAsync(CancellationToken ct)
    {
        try
        {
            await foreach ((long pos, TEntry entry) in _scrivener.TailAsync(0, ct))
            {
                // Append to current snapshot
                _snapshot.Add((pos, entry));

                if (_predicate.ShouldFlush(_snapshot))
                {
                    IReadOnlyList<(long position, TEntry entry)>? toFlush = null;
                    lock (_lock)
                    {
                        if (_predicate.ShouldFlush(_snapshot))
                        {
                            // swap snapshot with a fresh list; preserve order
                            toFlush = [.. _snapshot];
                            _snapshot.Clear();
                        }
                    }

                    if (toFlush is not null)
                    {
                        await _flushQueue.Writer.WriteAsync(toFlush, ct).ConfigureAwait(false);
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
            // On completion/cancel, push any remaining snapshot
            IReadOnlyList<(long position, TEntry entry)>? remainder = null;
            lock (_lock)
            {
                if (_snapshot.Count > 0)
                {
                    remainder = [.. _snapshot];
                    _snapshot.Clear();
                }
            }
            if (remainder is not null)
            {
                try
                {
                    _flushQueue.Writer.TryWrite(remainder);
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
            ChannelReader<IReadOnlyList<(long position, TEntry entry)>> reader = _flushQueue.Reader;
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out IReadOnlyList<(long position, TEntry entry)>? batch))
                {
                    await _sink.AppendSnapshotAsync(batch, ct).ConfigureAwait(false);
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
