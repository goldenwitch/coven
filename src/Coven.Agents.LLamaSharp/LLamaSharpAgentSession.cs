// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Coordinates a LLamaSharp agent session bridging the LLamaSharp and Agent journals.
/// Uses imbuing transmuters to carry the source journal position as a reagent for position-based ACKs.
/// </summary>
internal sealed class LLamaSharpAgentSession(
    ILLamaSharpGatewayConnection gateway,
    IScrivener<LLamaSharpEntry> llamaSharpJournal,
    IScrivener<AgentEntry> agentJournal,
    IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry> efferentTransmuter,
    ILogger<LLamaSharpAgentSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly ILLamaSharpGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<LLamaSharpEntry> _llamaSharpJournal = llamaSharpJournal ?? throw new ArgumentNullException(nameof(llamaSharpJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _llamaSharpToAgentsPump;
    private Task? _agentsToLLamaSharpPump;

    // Faults as soon as either pump does, so the daemon can report it rather than leaving the
    // caller waiting on a turn that is already dead.
    internal Task Completion => _llamaSharpToAgentsPump is not null && _agentsToLLamaSharpPump is not null
        ? DaemonPumps.WhenAllOrFirstFault(_llamaSharpToAgentsPump, _agentsToLLamaSharpPump)
        : Task.CompletedTask;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync().ConfigureAwait(false);

        _llamaSharpToAgentsPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, LLamaSharpEntry entry) in _llamaSharpJournal.TailAsync(0, ct))
                {
                    if (entry is LLamaSharpAck)
                    {
                        continue;
                    }

                    LLamaSharpLog.LLamaSharpToAgentsObserved(_logger, entry.GetType().Name, position);

                    AgentEntry agent = await _afferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    LLamaSharpLog.LLamaSharpToAgentsTransmuted(_logger, entry.GetType().Name, agent.GetType().Name);
                    long agentPos = await _agentJournal.WriteAsync(agent, ct).ConfigureAwait(false);
                    LLamaSharpLog.LLamaSharpToAgentsAppended(_logger, agent.GetType().Name, agentPos);
                }
                LLamaSharpLog.LLamaSharpToAgentsPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                LLamaSharpLog.LLamaSharpToAgentsPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                LLamaSharpLog.LLamaSharpToAgentsPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _agentsToLLamaSharpPump = Task.Run(async () =>
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

                    LLamaSharpLog.AgentsToLLamaSharpObserved(_logger, entry.GetType().Name, position);
                    LLamaSharpEntry llamaSharp = await _efferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    LLamaSharpLog.AgentsToLLamaSharpTransmuted(_logger, entry.GetType().Name, llamaSharp.GetType().Name);
                    long llamaSharpPos = await _llamaSharpJournal.WriteAsync(llamaSharp, ct).ConfigureAwait(false);
                    LLamaSharpLog.AgentsToLLamaSharpAppended(_logger, llamaSharp.GetType().Name, llamaSharpPos);
                }
                LLamaSharpLog.AgentsToLLamaSharpPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                LLamaSharpLog.AgentsToLLamaSharpPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                LLamaSharpLog.AgentsToLLamaSharpPumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_llamaSharpToAgentsPump is not null && _agentsToLLamaSharpPump is not null)
            {
                try
                {
                    await Completion.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // cooperative shutdown
                }
                catch (Exception)
                {
                    // Already reported by the daemon's monitor; disposal must still finish.
                }
            }
        }
        finally
        {
            _llamaSharpToAgentsPump = null;
            _agentsToLLamaSharpPump = null;
            await _gateway.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
