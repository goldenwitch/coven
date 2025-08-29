namespace Coven.Spellcasting;

using System.Collections.Generic;

public sealed record Spellbook<TSpell>(
    TSpell Payload,
    IReadOnlyDictionary<string, object?>? Meta = null
);

