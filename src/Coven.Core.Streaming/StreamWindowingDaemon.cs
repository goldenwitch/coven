// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Scrivener;
using Coven.Daemonology;
using Coven.Transmutation;

namespace Coven.Core.Streaming;

/// <summary>
/// Generic daemon that windows a stream of journal entries, emitting outputs based on a policy.
/// </summary>
/// <typeparam name="TEntry">Journal entry base type.</typeparam>
/// <typeparam name="TChunk">Chunk entry type to window.</typeparam>
/// <typeparam name="TOutput">Output entry type written when a window emits.</typeparam>
/// <typeparam name="TCompleted">Completion marker entry type that flushes buffers.</typeparam>
public sealed class StreamWindowingDaemon<TEntry, TChunk, TOutput, TCompleted>(
    IScrivener<DaemonEvent> daemonEvents,
    IScrivener<TEntry> journal,
    IWindowPolicy<TChunk> windowPolicy,
    IBatchTransmuter<TChunk, TOutput> batchTransmuter,
    IShatterPolicy<TEntry>? shatterPolicy = null
) : ContractDaemon(daemonEvents), IAsyncDisposable
    where TEntry : notnull
    where TChunk : TEntry
    where TOutput : TEntry
    where TCompleted : TEntry
{
    private readonly IScrivener<TEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly IWindowPolicy<TChunk> _windowPolicy = windowPolicy ?? throw new ArgumentNullException(nameof(windowPolicy));
    private readonly IBatchTransmuter<TChunk, TOutput> _batchTransmuter = batchTransmuter ?? throw new ArgumentNullException(nameof(batchTransmuter));
    private readonly IShatterPolicy<TEntry>? _shatterPolicy = shatterPolicy;
    private CancellationTokenSource? _linkedCancellationSource;
    private Task? _pumpTask;

    /// <summary>
    /// Starts the daemon and begins tailing the journal.
    /// </summary>
    public override async Task Start(CancellationToken cancellationToken)
    {
        _linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken linkedToken = _linkedCancellationSource.Token;

        long startPosition = 0;
        await foreach ((long position, _) in _journal.ReadBackwardAsync(long.MaxValue, linkedToken))
        {
            startPosition = position;
            break;
        }

        _pumpTask = Task.Run(() => RunAsync(startPosition, linkedToken), linkedToken);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates cooperative shutdown and awaits the pump loop completion.
    /// </summary>
    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _linkedCancellationSource?.Cancel();
        if (_pumpTask is not null)
        {
            try
            {
                await _pumpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // cooperative shutdown
            }
            finally
            {
                _pumpTask = null;
            }
        }
        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(long startAfterPosition, CancellationToken cancellationToken)
    {
        List<TChunk> buffer = [];
        int lookbackCount = Math.Max(1, _windowPolicy.MinChunkLookback);

        long lastEmittedPosition = startAfterPosition;
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset lastEmitAt = startedAt;
        long lastObservedChunkPosition = startAfterPosition;

        try
        {
            await foreach ((long position, TEntry entry) in _journal.TailAsync(startAfterPosition, cancellationToken))
            {
                switch (entry)
                {
                    case TChunk chunk:
                        lastObservedChunkPosition = position;
                        buffer.Add(chunk);

                        IEnumerable<TChunk> windowChunks = buffer.Count <= lookbackCount
                            ? buffer
                            : buffer.Skip(buffer.Count - lookbackCount);

                        if (_windowPolicy.ShouldEmit(new StreamWindow<TChunk>(windowChunks, buffer.Count, startedAt, lastEmitAt)))
                        {
                            await EmitBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
                            lastEmittedPosition = position;
                            lastEmitAt = DateTimeOffset.UtcNow;
                        }
                        break;
                    case TCompleted:
                        long flushUpToPosition = lastObservedChunkPosition > lastEmittedPosition ? lastObservedChunkPosition : lastEmittedPosition;
                        if (flushUpToPosition > lastEmittedPosition && buffer.Count > 0)
                        {
                            // Drain the buffer fully on completion, emitting as many outputs as needed
                            while (buffer.Count > 0)
                            {
                                int before = buffer.Count;
                                await EmitBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
                                if (buffer.Count >= before)
                                {
                                    break; // prevent infinite loop if transmuter does not reduce buffer
                                }
                            }
                            lastEmittedPosition = flushUpToPosition;
                            lastEmitAt = DateTimeOffset.UtcNow;
                        }
                        break;
                    default:
                        // ignore other entry types
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // cooperative shutdown
        }
        catch (Exception ex)
        {
            await Fail(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EmitBufferAsync(List<TChunk> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Count == 0)
        {
            return;
        }

        BatchTransmuteResult<TChunk, TOutput> result = await _batchTransmuter.Transmute(buffer, cancellationToken).ConfigureAwait(false);

        if (_shatterPolicy is not null)
        {
            bool any = false;
            IEnumerable<TEntry> shards = _shatterPolicy.Shatter(result.Output) ?? [];
            foreach (TEntry shard in shards)
            {
                any = true;
                await _journal.WriteAsync(shard, cancellationToken).ConfigureAwait(false);
            }

            if (!any)
            {
                // No shatter output produced; forward original output as-is
                await _journal.WriteAsync(result.Output, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await _journal.WriteAsync(result.Output, cancellationToken).ConfigureAwait(false);
        }

        if (result.HasRemainder && result.Remainder is not null)
        {
            TChunk remainder = result.Remainder;
            buffer.Clear();
            buffer.Add(remainder);
        }
        else
        {
            buffer.Clear();
        }
    }

    /// <summary>
    /// Ensures the daemon is shut down and disposes resources.
    /// </summary>
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
            _linkedCancellationSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
