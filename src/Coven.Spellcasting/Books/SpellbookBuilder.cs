namespace Coven.Spellcasting;

using Coven.Spellcasting.Spells;

public sealed class SpellbookBuilder
{
    private readonly List<SpellDefinition> _definitions = new();
    private readonly List<object> _spells = new();

    public SpellbookBuilder AddSpell(ISpell spell)
    {
        if (spell is null) throw new ArgumentNullException(nameof(spell));
        _spells.Add(spell);
        _definitions.Add(new SpellDefinition(SchemaGen.GetFriendlyName(spell.GetType())));
        return this;
    }

    public SpellbookBuilder AddSpell<TIn>(ISpell<TIn> spell)
    {
        if (spell is null) throw new ArgumentNullException(nameof(spell));
        _spells.Add(spell);
        _definitions.Add(spell.GetDefinition());
        return this;
    }

    public SpellbookBuilder AddSpell<TIn, TOut>(ISpell<TIn, TOut> spell)
    {
        if (spell is null) throw new ArgumentNullException(nameof(spell));
        _spells.Add(spell);
        _definitions.Add(spell.GetDefinition());
        return this;
    }

    public Spellbook Build()
    {
        return new Spellbook(_definitions.AsReadOnly(), _spells.AsReadOnly());
    }
}

