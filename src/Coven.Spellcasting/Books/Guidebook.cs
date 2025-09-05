using System.Collections.ObjectModel;

namespace Coven.Spellcasting;

public record Guidebook(string Instructions, ReadOnlyDictionary<string, string> UriMap) : IBook;
