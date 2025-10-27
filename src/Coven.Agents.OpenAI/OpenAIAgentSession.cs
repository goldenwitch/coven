// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIAgentSession(
    IOpenAIGatewayConnection gateway,
    IScrivener<OpenAIEntry> openAIJournal,
    IScrivener<AgentEntry> agentJournal,
    IBiDirectionalTransmuter<OpenAIEntry, AgentEntry> transmuter,
    ILogger<OpenAIAgentSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly IOpenAIGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<OpenAIEntry> _openAIJournal = openAIJournal ?? throw new ArgumentNullException(nameof(openAIJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IBiDirectionalTransmuter<OpenAIEntry, AgentEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _openAIToAgentsPump;
    private Task? _agentsToOpenAIPump;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync().ConfigureAwait(false);

        _openAIToAgentsPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, OpenAIEntry entry) in _openAIJournal.TailAsync(0, ct))
                {
                    if (entry is OpenAIAck)
                    {
                        continue;
                    }

                    OpenAILog.OpenAIToAgentsObserved(_logger, entry.GetType().Name, position);
                    AgentEntry agent = await _transmuter.TransmuteAfferent(entry, ct).ConfigureAwait(false);
                    OpenAILog.OpenAIToAgentsTransmuted(_logger, entry.GetType().Name, agent.GetType().Name);
                    long agentPos = await _agentJournal.WriteAsync(agent, ct).ConfigureAwait(false);
                    OpenAILog.OpenAIToAgentsAppended(_logger, agent.GetType().Name, agentPos);
                }
                OpenAILog.OpenAIToAgentsPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                OpenAILog.OpenAIToAgentsPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                OpenAILog.OpenAIToAgentsPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _agentsToOpenAIPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, AgentEntry entry) in _agentJournal.TailAsync(0, ct))
                {
                    if (entry is AgentAck)
                    {
                        continue;
                    }

                    OpenAILog.AgentsToOpenAIObserved(_logger, entry.GetType().Name, position);
                    OpenAIEntry openAI = await _transmuter.TransmuteEfferent(entry, ct).ConfigureAwait(false);
                    OpenAILog.AgentsToOpenAITransmuted(_logger, entry.GetType().Name, openAI.GetType().Name);
                    long aiPos = await _openAIJournal.WriteAsync(openAI, ct).ConfigureAwait(false);
                    OpenAILog.AgentsToOpenAIAppended(_logger, openAI.GetType().Name, aiPos);
                }
                OpenAILog.AgentsToOpenAIPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                OpenAILog.AgentsToOpenAIPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                OpenAILog.AgentsToOpenAIPumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_openAIToAgentsPump is not null && _agentsToOpenAIPump is not null)
            {
                try
                {
                    await Task.WhenAll(_openAIToAgentsPump, _agentsToOpenAIPump).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // cooperative shutdown
                }
            }
        }
        finally
        {
            _openAIToAgentsPump = null;
            _agentsToOpenAIPump = null;
            GC.SuppressFinalize(this);
        }
    }
}
