namespace Coven.Spellcasting.Agents.Validation;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Idempotent readiness for a single agent (e.g., install CLI, check versions).
/// Implementations MUST be safe to call repeatedly.
/// </summary>
public interface IAgentValidation
{
    /// <summary>
    /// Stable identifier for the agent, e.g., "codex".
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Validate and, if needed, provision the agent environment.
    /// Must be idempotent.
    /// </summary>
    Task<AgentValidationResult> ValidateAsync(
        Spellcasting.Agents.SpellContext? context = null,
        CancellationToken ct = default);
}

