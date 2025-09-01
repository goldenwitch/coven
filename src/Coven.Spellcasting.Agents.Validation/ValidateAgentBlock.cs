namespace Coven.Spellcasting.Agents.Validation;

using System.Threading;
using System.Threading.Tasks;
using Coven.Core;

/// <summary>
/// Convenience MagikBlock to run agent validation inside a ritual.
/// </summary>
public sealed class ValidateAgentBlock : IMagikBlock<Empty, Empty>
{
    private readonly IAgentValidation _validator;

    public ValidateAgentBlock(IAgentValidation validator)
        => _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    public async Task<Empty> DoMagik(Empty input)
    {
        // Run validation; ignore outcome to keep block non-failing by default.
        // Implementations may log or track state inside the validator.
        await _validator.ValidateAsync(CancellationToken.None).ConfigureAwait(false);
        return input;
    }
}

