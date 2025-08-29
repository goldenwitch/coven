namespace Coven.Spellcasting;

public sealed record Spellbook<TSpell>(TSpell Payload) : IBook<TSpell>;
