using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Grimoire;

public class Ask : ISpell<string, string>
{
    public Ask()
    {
        
    }

    public Task<string> CastSpell(string Input)
    {
        throw new NotImplementedException();
    }
}
