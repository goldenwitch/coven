// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Validation;

public enum AgentValidationOutcome
{
    /// <summary>
    /// Nothing was done because the environment is already ready (via stamp/probe).
    /// </summary>
    Noop,

    /// <summary>
    /// Provisioning was performed and environment is now ready.
    /// </summary>
    Performed,

    /// <summary>
    /// Skipped due to insufficient permissions or other non-fatal precondition.
    /// </summary>
    Skipped
}

public sealed record AgentValidationResult(
    AgentValidationOutcome Outcome,
    string? Message = null)
{
    public static AgentValidationResult Noop(string? message = null)
        => new(AgentValidationOutcome.Noop, message);

    public static AgentValidationResult Performed(string? message = null)
        => new(AgentValidationOutcome.Performed, message);

    public static AgentValidationResult Skipped(string? message = null)
        => new(AgentValidationOutcome.Skipped, message);
}
