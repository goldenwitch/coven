namespace Coven.Spellcasting;

using Coven.Spellcasting.Spells;

public sealed record Spellbook(
    IReadOnlyList<SpellDefinition> Definitions,
    IReadOnlyList<object> Spells
) : IBook;
