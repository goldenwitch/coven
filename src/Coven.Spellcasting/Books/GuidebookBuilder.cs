namespace Coven.Spellcasting;

using System.Collections.ObjectModel;

public sealed class GuidebookBuilder
{
    private readonly List<string> _sections = new();
    private readonly Dictionary<string, string> _uriMap = new(StringComparer.OrdinalIgnoreCase);

    public GuidebookBuilder AddText(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return this;
        _sections.Add(content);
        return this;
    }

    public GuidebookBuilder AddFile(string path, string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return this;
        try
        {
            var full = Path.GetFullPath(path);
            // Include the file's contents inline for agents without URI fetching
            var text = File.ReadAllText(full);
            if (!string.IsNullOrWhiteSpace(text)) _sections.Add(text);

            // Also expose as a file URI in the map for agents that resolve URIs
            var name = string.IsNullOrWhiteSpace(alias) ? Path.GetFileName(full) : alias!;
            var uri = new Uri(full).AbsoluteUri; // file://...
            if (!_uriMap.ContainsKey(name)) _uriMap[name] = uri;
        }
        catch
        {
            // Ignore IO issues for now; caller may add multiple sources
        }
        return this;
    }

    public GuidebookBuilder AddUri(string name, string uri)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uri)) return this;
        if (!_uriMap.ContainsKey(name)) _uriMap[name] = uri;
        return this;
    }

    public Guidebook Build()
    {
        var instructions = _sections.Count == 0 ? string.Empty : string.Join("\n\n", _sections);
        var rom = new ReadOnlyDictionary<string, string>(_uriMap);
        return new Guidebook(instructions, rom);
    }
}

