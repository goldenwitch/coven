// SPDX-License-Identifier: BUSL-1.1
using System.Text;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Agents.Streaming;

public sealed class AgentStreamSegmentationDaemon(
    IScrivener<DaemonEvent> daemonEvents,
    IScrivener<AgentEntry> agentJournal,
    IStreamSegmenter segmenter) : ContractDaemon(daemonEvents), IAsyncDisposable
{
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IStreamSegmenter _segmenter = segmenter ?? throw new ArgumentNullException(nameof(segmenter));

    private CancellationTokenSource? _linkedCancellationSource;
    private Task? _pumpTask;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken linkedToken = _linkedCancellationSource.Token;

        // Determine start position (logical end at start time)
        long startPosition = 0;
        await foreach ((long position, _) in _agentJournal.ReadBackwardAsync(long.MaxValue, linkedToken))
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
        Queue<string> pendingChunks = new();
        int lookbackCount = Math.Max(1, _segmenter.MinChunkLookback);

        long lastEmittedPosition = startAfterPosition;
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset lastEmitAt = startedAt;
        long lastObservedChunkPosition = startAfterPosition;

        try
        {
            await foreach ((long position, AgentEntry entry) in _agentJournal.TailAsync(startAfterPosition, cancellationToken))
            {
                switch (entry)
                {
                    case AgentChunk chunk:
                        lastObservedChunkPosition = position;
                        pendingChunks.Enqueue(chunk.Text);
                        while (pendingChunks.Count > lookbackCount)
                        {
                            pendingChunks.Dequeue();
                        }
                        if (_segmenter.ShouldEmit(new StreamWindow(pendingChunks, (int)(lastObservedChunkPosition - lastEmittedPosition), startedAt, lastEmitAt)))
                        {
                            await EmitAsync(lastEmittedPosition, position, cancellationToken).ConfigureAwait(false);
                            lastEmittedPosition = position;
                            lastEmitAt = DateTimeOffset.UtcNow;
                            pendingChunks.Clear();
                        }
                        break;
                    case AgentStreamCompleted completed:
                        // Flush any remaining chunks up to the completion event position
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
                        // ignore AgentResponse/AgentPrompt/AgentThought/Ack
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
        await foreach ((long position, AgentEntry entry) in _agentJournal.TailAsync(fromExclusive, cancellationToken))
        {
            if (position > toInclusive)
            {
                break;
            }
            if (entry is AgentChunk chunkEntry)
            {
                // Track sender from the latest chunk we see in-range
                if (!string.IsNullOrEmpty(chunkEntry.Sender))
                {
                    sender = chunkEntry.Sender;
                }
                if (!string.IsNullOrEmpty(chunkEntry.Text))
                {
                    stringBuilder.Append(chunkEntry.Text);
                }
            }
        }

        string text = stringBuilder.ToString();
        if (text.Length == 0)
        {
            return;
        }
        // Preserve the originating sender to keep this daemon agent-agnostic
        await _agentJournal.WriteAsync(new AgentResponse(sender, text), cancellationToken).ConfigureAwait(false);
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
