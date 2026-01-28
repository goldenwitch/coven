// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Claude;

/// <summary>
/// Factory for creating Claude agent sessions with all required dependencies.
/// </summary>
internal sealed class ClaudeAgentSessionFactory(
    IClaudeGatewayConnection gateway,
    IScrivener<ClaudeEntry> claudeJournal,
    IScrivener<AgentEntry> agentJournal,
    IShatterPolicy<ClaudeEntry> shatterPolicy,
    IImbuingTransmuter<ClaudeEntry, long, AgentEntry> afferentTransmuter,
    IImbuingTransmuter<AgentEntry, long, ClaudeEntry> efferentTransmuter,
    ILogger<ClaudeAgentSession> logger)
{
    private readonly IClaudeGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<ClaudeEntry> _claudeJournal = claudeJournal ?? throw new ArgumentNullException(nameof(claudeJournal));
    private readonly IScrivener<AgentEntry> _agentJournal = agentJournal ?? throw new ArgumentNullException(nameof(agentJournal));
    private readonly IShatterPolicy<ClaudeEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly IImbuingTransmuter<ClaudeEntry, long, AgentEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<AgentEntry, long, ClaudeEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger<ClaudeAgentSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ClaudeAgentSession Create(CancellationToken sessionToken)
    {
        return new ClaudeAgentSession(
            _gateway,
            _claudeJournal,
            _agentJournal,
            _shatterPolicy,
            _afferentTransmuter,
            _efferentTransmuter,
            _logger,
            sessionToken);
    }
}
