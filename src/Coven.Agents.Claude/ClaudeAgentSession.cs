// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Claude;

/// <summary>
/// Coordinates a Claude agent session bridging the Claude and Agent journals.
/// Uses imbuing transmuters to carry the source journal position as a reagent for position-based ACKs.
/// </summary>
internal sealed class ClaudeAgentSession(
    IClaudeGatewayConnection gateway,
    IScrivener<ClaudeEntry> claudeJournal,
    IScrivener<AgentEntry> agentJournal,
    IShatterPolicy<ClaudeEntry> shatterPolicy,
    IImbuingTransmuter<ClaudeEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, ClaudeEntry> efferentTransmuter,
    ILogger<ClaudeAgentSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly IClaudeGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<ClaudeEntry> _claudeJournal = claudeJournal ?? throw new ArgumentNullException(nameof(claudeJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IShatterPolicy<ClaudeEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly IImbuingTransmuter<ClaudeEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, ClaudeEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _claudeToAgentsPump;
    private Task? _agentsToClaudePump;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync().ConfigureAwait(false);

        _claudeToAgentsPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, ClaudeEntry entry) in _claudeJournal.TailAsync(0, ct))
                {
                    if (entry is ClaudeAck)
                    {
                        continue;
                    }

                    ClaudeLog.ClaudeToAgentsObserved(_logger, entry.GetType().Name, position);

                    // Session-local shattering for Claude thinking chunks on paragraph boundary
                    if (entry is ClaudeAfferentThinkingChunk)
                    {
                        bool produced = false;
                        IEnumerable<ClaudeEntry> outputs = _shatterPolicy.Shatter(entry) ?? [];
                        foreach (ClaudeEntry claudeEntry in outputs)
                        {
                            if (claudeEntry is ClaudeAfferentThinkingChunk)
                            {
                                produced = true;
                                AgentEntry agentChunk = await _afferentTransmuter.Transmute(claudeEntry, position, ct).ConfigureAwait(false);
                                long pos = await _agentJournal.WriteAsync(agentChunk, ct).ConfigureAwait(false);
                                ClaudeLog.ClaudeToAgentsAppended(_logger, agentChunk.GetType().Name, pos);
                            }
                        }

                        if (produced)
                        {
                            continue;
                        }
                    }

                    AgentEntry agent = await _afferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    ClaudeLog.ClaudeToAgentsTransmuted(_logger, entry.GetType().Name, agent.GetType().Name);
                    long agentPos = await _agentJournal.WriteAsync(agent, ct).ConfigureAwait(false);
                    ClaudeLog.ClaudeToAgentsAppended(_logger, agent.GetType().Name, agentPos);
                }
                ClaudeLog.ClaudeToAgentsPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                ClaudeLog.ClaudeToAgentsPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                ClaudeLog.ClaudeToAgentsPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _agentsToClaudePump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, AgentEntry entry) in _agentJournal.TailAsync(0, ct))
                {
                    // Early filtering: ignore drafts and acks to avoid loops/noise
                    if (entry is IDraft or AgentAck)
                    {
                        continue;
                    }

                    ClaudeLog.AgentsToClaudeObserved(_logger, entry.GetType().Name, position);
                    ClaudeEntry claude = await _efferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    ClaudeLog.AgentsToClaudeTransmuted(_logger, entry.GetType().Name, claude.GetType().Name);
                    long claudePos = await _claudeJournal.WriteAsync(claude, ct).ConfigureAwait(false);
                    ClaudeLog.AgentsToClaudeAppended(_logger, claude.GetType().Name, claudePos);
                }
                ClaudeLog.AgentsToClaudePumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                ClaudeLog.AgentsToClaudePumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                ClaudeLog.AgentsToClaudePumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_claudeToAgentsPump is not null && _agentsToClaudePump is not null)
            {
                try
                {
                    await Task.WhenAll(_claudeToAgentsPump, _agentsToClaudePump).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // cooperative shutdown
                }
            }
        }
        finally
        {
            _claudeToAgentsPump = null;
            _agentsToClaudePump = null;
            GC.SuppressFinalize(this);
        }
    }
}
