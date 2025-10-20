// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIAgentSessionFactory(
    OpenAIGatewayConnection gatewayConnection,
    IScrivener<OpenAIEntry> openAIJournal,
    IScrivener<AgentEntry> agentJournal,
    IBiDirectionalTransmuter<OpenAIEntry, AgentEntry> transmuter,
    ILogger<OpenAIAgentSession> logger)
{
    private readonly OpenAIGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<OpenAIEntry> _openAIJournal = openAIJournal ?? throw new ArgumentNullException(nameof(openAIJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IBiDirectionalTransmuter<OpenAIEntry, AgentEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly ILogger<OpenAIAgentSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public OpenAIAgentSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _openAIJournal, _agentJournal, _transmuter, _logger, sessionToken);
}

