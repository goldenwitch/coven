// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

/// <summary>
/// Constructs <see cref="GeminiAgentSession"/> instances with imbuing transmuters for position-based acknowledgements.
/// </summary>
internal sealed class GeminiAgentSessionFactory(
    IGeminiGatewayConnection gatewayConnection,
    IScrivener<GeminiEntry> geminiJournal,
    IScrivener<AgentEntry> agentJournal,
    IImbuingTransmuter<GeminiEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, GeminiEntry> efferentTransmuter,
    IShatterPolicy<GeminiEntry> shatterPolicy,
    ILogger<GeminiAgentSession> logger)
{
    private readonly IGeminiGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<GeminiEntry> _geminiJournal = geminiJournal ?? throw new ArgumentNullException(nameof(geminiJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IImbuingTransmuter<GeminiEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, GeminiEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly IShatterPolicy<GeminiEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly ILogger<GeminiAgentSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public GeminiAgentSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _geminiJournal, _agentJournal, _shatterPolicy, _afferentTransmuter, _efferentTransmuter, _logger, sessionToken);
}
