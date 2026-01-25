// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

/// <summary>
/// Coordinates a Gemini agent session bridging the Gemini and Agent journals.
/// Uses imbuing transmuters to carry the source journal position as a reagent for position-based ACKs.
/// </summary>
internal sealed class GeminiAgentSession(
    IGeminiGatewayConnection gateway,
    IScrivener<GeminiEntry> geminiJournal,
    IScrivener<AgentEntry> agentJournal,
    IShatterPolicy<GeminiEntry> shatterPolicy,
    IImbuingTransmuter<GeminiEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, GeminiEntry> efferentTransmuter,
    ILogger<GeminiAgentSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly IGeminiGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<GeminiEntry> _geminiJournal = geminiJournal ?? throw new ArgumentNullException(nameof(geminiJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IShatterPolicy<GeminiEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly IImbuingTransmuter<GeminiEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, GeminiEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _geminiToAgentsPump;
    private Task? _agentsToGeminiPump;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync().ConfigureAwait(false);

        _geminiToAgentsPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, GeminiEntry entry) in _geminiJournal.TailAsync(0, ct))
                {
                    if (entry is GeminiAck)
                    {
                        continue;
                    }

                    GeminiLog.GeminiToAgentsObserved(_logger, entry.GetType().Name, position);

                    if (entry is GeminiAfferentReasoningChunk)
                    {
                        bool produced = false;
                        IEnumerable<GeminiEntry> outputs = _shatterPolicy.Shatter(entry) ?? [];
                        foreach (GeminiEntry geminiEntry in outputs)
                        {
                            if (geminiEntry is GeminiAfferentReasoningChunk)
                            {
                                produced = true;
                                AgentEntry agentChunk = await _afferentTransmuter.Transmute(geminiEntry, position, ct).ConfigureAwait(false);
                                long pos = await _agentJournal.WriteAsync(agentChunk, ct).ConfigureAwait(false);
                                GeminiLog.GeminiToAgentsAppended(_logger, agentChunk.GetType().Name, pos);
                            }
                        }

                        if (produced)
                        {
                            continue;
                        }
                    }

                    AgentEntry agent = await _afferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    GeminiLog.GeminiToAgentsTransmuted(_logger, entry.GetType().Name, agent.GetType().Name);
                    long agentPos = await _agentJournal.WriteAsync(agent, ct).ConfigureAwait(false);
                    GeminiLog.GeminiToAgentsAppended(_logger, agent.GetType().Name, agentPos);
                }
                GeminiLog.GeminiToAgentsPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                GeminiLog.GeminiToAgentsPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                GeminiLog.GeminiToAgentsPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _agentsToGeminiPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, AgentEntry entry) in _agentJournal.TailAsync(0, ct))
                {
                    if (entry is IDraft or AgentAck)
                    {
                        continue;
                    }

                    GeminiLog.AgentsToGeminiObserved(_logger, entry.GetType().Name, position);
                    GeminiEntry gemini = await _efferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    GeminiLog.AgentsToGeminiTransmuted(_logger, entry.GetType().Name, gemini.GetType().Name);
                    long geminiPos = await _geminiJournal.WriteAsync(gemini, ct).ConfigureAwait(false);
                    GeminiLog.AgentsToGeminiAppended(_logger, gemini.GetType().Name, geminiPos);
                }
                GeminiLog.AgentsToGeminiPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                GeminiLog.AgentsToGeminiPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                GeminiLog.AgentsToGeminiPumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_geminiToAgentsPump is not null && _agentsToGeminiPump is not null)
            {
                try
                {
                    await Task.WhenAll(_geminiToAgentsPump, _agentsToGeminiPump).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            _geminiToAgentsPump = null;
            _agentsToGeminiPump = null;
            GC.SuppressFinalize(this);
        }
    }
}
