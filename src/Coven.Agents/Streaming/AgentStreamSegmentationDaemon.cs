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

    private CancellationTokenSource? _cts;
    private Task? _pump;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        // Determine start position (logical end at start time)
        long startPos = 0;
        await foreach ((long pos, _) in _agentJournal.ReadBackwardAsync(long.MaxValue, ct))
        {
            startPos = pos;
            break;
        }

        _pump = Task.Run(() => RunAsync(startPos, ct), ct);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_pump is not null)
        {
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // cooperative shutdown
            }
            finally
            {
                _pump = null;
            }
        }
        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(long startAfterPosition, CancellationToken ct)
    {
        Queue<string> pending = new();
        int lookback = Math.Max(1, _segmenter.MinChunkLookback);

        long lastEmittedPos = startAfterPosition;
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset lastEmitAt = startedAt;
        long lastObservedChunkPos = startAfterPosition;

        try
        {
            await foreach ((long pos, AgentEntry entry) in _agentJournal.TailAsync(startAfterPosition, ct))
            {
                switch (entry)
                {
                    case AgentChunk chunk:
                        lastObservedChunkPos = pos;
                        pending.Enqueue(chunk.Text);
                        while (pending.Count > lookback)
                        {
                            pending.Dequeue();
                        }
                        if (_segmenter.ShouldEmit(new StreamWindow(pending, (int)(lastObservedChunkPos - lastEmittedPos), startedAt, lastEmitAt)))
                        {
                            await EmitAsync(lastEmittedPos, pos, ct).ConfigureAwait(false);
                            lastEmittedPos = pos;
                            lastEmitAt = DateTimeOffset.UtcNow;
                            pending.Clear();
                        }
                        break;
                    case AgentStreamCompleted completed:
                        // Flush any remaining chunks up to the completion event position
                        long flushUpTo = lastObservedChunkPos > lastEmittedPos ? lastObservedChunkPos : lastEmittedPos;
                        if (flushUpTo > lastEmittedPos)
                        {
                            await EmitAsync(lastEmittedPos, flushUpTo, ct).ConfigureAwait(false);
                            lastEmittedPos = flushUpTo;
                            lastEmitAt = DateTimeOffset.UtcNow;
                            pending.Clear();
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
            await Fail(ex, ct).ConfigureAwait(false);
        }
    }

    private async Task EmitAsync(long fromExclusive, long toInclusive, CancellationToken ct)
    {
        StringBuilder sb = new();
        string sender = string.Empty;
        await foreach ((long pos, AgentEntry entry) in _agentJournal.TailAsync(fromExclusive, ct))
        {
            if (pos > toInclusive)
            {
                break;
            }
            if (entry is AgentChunk c)
            {
                // Track sender from the latest chunk we see in-range
                if (!string.IsNullOrEmpty(c.Sender))
                {
                    sender = c.Sender;
                }
                if (!string.IsNullOrEmpty(c.Text))
                {
                    sb.Append(c.Text);
                }
            }
        }

        string text = sb.ToString();
        if (text.Length == 0)
        {
            return;
        }
        // Preserve the originating sender to keep this daemon agent-agnostic
        await _agentJournal.WriteAsync(new AgentResponse(sender, text), ct).ConfigureAwait(false);
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
            GC.SuppressFinalize(this);
        }
    }
}
