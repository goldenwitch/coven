// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Daemonology;
using Coven.Transmutation;

namespace Coven.Core.Streaming;

public sealed class StreamSegmentationDaemon<TEntry, TChunk, TOutput, TCompleted>(
    IScrivener<DaemonEvent> daemonEvents,
    IScrivener<TEntry> journal,
    IStreamSegmenter<TChunk> segmenter,
    ITransmuter<TChunk, (string Sender, string Text)> chunkTransmuter,
    ITransmuter<(string Sender, string Text), TOutput> outputTransmuter
) : ContractDaemon(daemonEvents), IAsyncDisposable
    where TEntry : notnull
    where TChunk : TEntry
    where TOutput : TEntry
    where TCompleted : TEntry
{
    private readonly IScrivener<TEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly IStreamSegmenter<TChunk> _segmenter = segmenter ?? throw new ArgumentNullException(nameof(segmenter));
    private readonly ITransmuter<TChunk, (string Sender, string Text)> _chunkTransmuter = chunkTransmuter ?? throw new ArgumentNullException(nameof(chunkTransmuter));
    private readonly ITransmuter<(string Sender, string Text), TOutput> _outputTransmuter = outputTransmuter ?? throw new ArgumentNullException(nameof(outputTransmuter));

    private CancellationTokenSource? _linkedCancellationSource;
    private Task? _pumpTask;

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
        Queue<TChunk> pendingChunks = new();
        int lookbackCount = Math.Max(1, _segmenter.MinChunkLookback);

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
                        pendingChunks.Enqueue(chunk);
                        while (pendingChunks.Count > lookbackCount)
                        {
                            pendingChunks.Dequeue();
                        }
                        if (_segmenter.ShouldEmit(new StreamWindow<TChunk>(pendingChunks, pendingChunks.Count, startedAt, lastEmitAt)))
                        {
                            await EmitAsync(lastEmittedPosition, position, cancellationToken).ConfigureAwait(false);
                            lastEmittedPosition = position;
                            lastEmitAt = DateTimeOffset.UtcNow;
                            pendingChunks.Clear();
                        }
                        break;
                    case TCompleted:
                        long flushUpToPosition = lastObservedChunkPosition > lastEmittedPosition ? lastObservedChunkPosition : lastEmittedPosition;
                        if (flushUpToPosition > lastEmittedPosition)
                        {
                            await EmitAsync(lastEmittedPosition, flushUpToPosition, cancellationToken).ConfigureAwait(false);
                            lastEmittedPosition = flushUpToPosition;
                            lastEmitAt = DateTimeOffset.UtcNow;
                            pendingChunks.Clear();
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

    private async Task EmitAsync(long fromExclusive, long toInclusive, CancellationToken cancellationToken)
    {
        StringBuilder stringBuilder = new();
        string sender = string.Empty;
        await foreach ((long position, TEntry entry) in _journal.TailAsync(fromExclusive, cancellationToken))
        {
            if (position > toInclusive)
            {
                break;
            }
            if (entry is TChunk chunkEntry)
            {
                (string Sender, string Text) = await _chunkTransmuter.Transmute(chunkEntry, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(Sender))
                {
                    sender = Sender;
                }
                if (!string.IsNullOrEmpty(Text))
                {
                    stringBuilder.Append(Text);
                }
            }
        }

        string text = stringBuilder.ToString();
        if (text.Length == 0)
        {
            return;
        }

        TOutput output = await _outputTransmuter.Transmute((sender, text), cancellationToken).ConfigureAwait(false);
        await _journal.WriteAsync(output, cancellationToken).ConfigureAwait(false);
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
            _linkedCancellationSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
