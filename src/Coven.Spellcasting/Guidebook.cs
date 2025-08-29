namespace Coven.Spellcasting;

using System.Collections.Generic;

public sealed record Guidebook<TGuide>(
    TGuide Payload,
    IReadOnlyDictionary<string, object?>? Meta = null
);

