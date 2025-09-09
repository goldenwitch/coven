using System.Collections.ObjectModel;

namespace Coven.Spellcasting;

public record Guidebook(
    ReadOnlyDictionary<string, string> Sections,
    ReadOnlyDictionary<string, string> UriMap
) : IBook;
