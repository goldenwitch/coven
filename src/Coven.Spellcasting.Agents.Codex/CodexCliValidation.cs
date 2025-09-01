using Coven.Spellcasting.Agents.Validation;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliValidation : IAgentValidation
{
    public string AgentId => "Codex";

    public Task<AgentValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

}
