using Coven.Spellcasting.Spells;
using Coven.Core;

namespace Coven.Spellcasting.Grimoire;

// Cancels the currently hosted agent loop.
public sealed class CancelAgent : ISpell
{
    public async Task CastSpell()
    {
        try
        {
            await AmbientAgent.CancelAsync().ConfigureAwait(false);
        }
        catch
        {
            // Intentionally swallow errors to avoid destabilizing the host.
        }
    }
}
