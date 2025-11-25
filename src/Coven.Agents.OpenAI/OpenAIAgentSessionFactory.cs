// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

/// <summary>
/// Constructs <see cref="OpenAIAgentSession"/> instances with imbuing transmuters for position-based acknowledgements.
/// </summary>
internal sealed class OpenAIAgentSessionFactory(
    IOpenAIGatewayConnection gatewayConnection,
    IScrivener<OpenAIEntry> openAIJournal,
    IScrivener<AgentEntry> agentJournal,
    IImbuingTransmuter<OpenAIEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, OpenAIEntry> efferentTransmuter,
    IShatterPolicy<OpenAIEntry> shatterPolicy,
    ILogger<OpenAIAgentSession> logger)
{
    private readonly IOpenAIGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<OpenAIEntry> _openAIJournal = openAIJournal ?? throw new ArgumentNullException(nameof(openAIJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IImbuingTransmuter<OpenAIEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, OpenAIEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly IShatterPolicy<OpenAIEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly ILogger<OpenAIAgentSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public OpenAIAgentSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _openAIJournal, _agentJournal, _shatterPolicy, _afferentTransmuter, _efferentTransmuter, _logger, sessionToken);
}
