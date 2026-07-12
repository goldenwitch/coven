// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Factory for creating <see cref="LLamaSharpAgentSession"/> instances with all required dependencies.
/// </summary>
internal sealed class LLamaSharpAgentSessionFactory(
    ILLamaSharpGatewayConnection gateway,
    IScrivener<LLamaSharpEntry> llamaSharpJournal,
    IScrivener<AgentEntry> agentJournal,
    IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry> efferentTransmuter,
    ILogger<LLamaSharpAgentSession> sessionLogger)
{
    private readonly ILLamaSharpGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<LLamaSharpEntry> _llamaSharpJournal = llamaSharpJournal ?? throw new ArgumentNullException(nameof(llamaSharpJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IImbuingTransmuter<LLamaSharpEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, LLamaSharpEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger<LLamaSharpAgentSession> _sessionLogger = sessionLogger ?? throw new ArgumentNullException(nameof(sessionLogger));

    public LLamaSharpAgentSession Create(CancellationToken sessionToken) =>
        new(_gateway, _llamaSharpJournal, _agentJournal, _afferentTransmuter, _efferentTransmuter, _sessionLogger, sessionToken);
}
