// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

public sealed class SpellbookBuilder
{
    private readonly List<SpellDefinition> _definitions = [];
    private readonly List<object> _spells = [];

    public SpellbookBuilder AddSpell(ISpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    public SpellbookBuilder AddSpell<TIn>(ISpell<TIn> spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    public SpellbookBuilder AddSpell<TIn, TOut>(ISpell<TIn, TOut> spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    public Spellbook Build()
    {
        return new Spellbook(_definitions.AsReadOnly(), _spells.AsReadOnly());
    }
}
