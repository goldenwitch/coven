namespace Coven.Spellcasting;

using System.Collections.Generic;

public sealed record Testbook<TTest>(
    TTest Payload,
    IReadOnlyDictionary<string, object?>? Meta = null
);

