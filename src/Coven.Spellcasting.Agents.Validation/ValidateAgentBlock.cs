namespace Coven.Spellcasting.Agents.Validation;

using System.Threading;
using System.Threading.Tasks;
using Coven.Core;

/// <summary>
/// Convenience MagikBlock to run agent validation inside a ritual.
/// Passes through the SpellContext unchanged.
/// </summary>
public sealed class ValidateAgentBlock : IMagikBlock<Spellcasting.Agents.SpellContext, Spellcasting.Agents.SpellContext>
{
    private readonly IAgentValidation _validator;

    public ValidateAgentBlock(IAgentValidation validator)
        => _validator = validator ?? throw new System.ArgumentNullException(nameof(validator));

    public async Task<Spellcasting.Agents.SpellContext> DoMagik(Spellcasting.Agents.SpellContext input)
    {
        // Run validation; ignore outcome to keep block non-failing by default.
        // Implementations may log or track state inside the validator.
        await _validator.ValidateAsync(input, CancellationToken.None).ConfigureAwait(false);
        return input;
    }
}

