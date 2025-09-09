namespace Coven.Spellcasting;

using System.Collections.ObjectModel;

public sealed class GuidebookBuilder
{
    private readonly Dictionary<string, string> _uriMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sectionMap = new(StringComparer.OrdinalIgnoreCase);

    public GuidebookBuilder AddText(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return this;
        var key = ($"Section {_sectionMap.Count + 1}");
        _sectionMap[key] = content;
        return this;
    }

    public GuidebookBuilder AddFile(string path, string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return this;
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Guidebook file not found", full);
        var text = File.ReadAllText(full);

        var title = string.IsNullOrWhiteSpace(alias) ? Path.GetFileName(full) : alias!;
        _sectionMap[title] = text ?? string.Empty;

        var name = title;
        var uri = new Uri(full).AbsoluteUri; // file://...
        if (!_uriMap.ContainsKey(name)) _uriMap[name] = uri;
        return this;
    }

    // Adds a titled section from a set of lines.
    public GuidebookBuilder AddSection(string title, IEnumerable<string> lines)
    {
        if (string.IsNullOrWhiteSpace(title)) return this;
        if (lines is null) return this;

        var list = new List<string>();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line)) list.Add(line.Trim());
        }

        var body = list.Count == 0 ? string.Empty : string.Join("\n", list);
        _sectionMap[title] = body;
        return this;
    }

    // Adds a titled section by reading the contents of a file.
    public GuidebookBuilder AddSectionFromFile(string title, string path)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(path)) return this;
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Guidebook file not found", full);
        var text = File.ReadAllText(full);
        _sectionMap[title] = text ?? string.Empty;
        return this;
    }

    // Bulk registration: dictionary of title->body
    public GuidebookBuilder AddSections(IDictionary<string, string> sections)
    {
        if (sections is null) return this;
        foreach (var kvp in sections)
        {
            AddSection(kvp.Key, new[] { kvp.Value });
        }
        return this;
    }

    // Bulk registration: params list of (title, body)
    public GuidebookBuilder AddSections(params (string Title, string Body)[] pairs)
    {
        if (pairs is null) return this;
        foreach (var p in pairs)
        {
            AddSection(p.Title, new[] { p.Body });
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
        if (_sectionMap.Count == 0) throw new InvalidOperationException("Guidebook has no sections configured.");
        var sections = new ReadOnlyDictionary<string, string>(_sectionMap);
        var uris = new ReadOnlyDictionary<string, string>(_uriMap);
        return new Guidebook(sections, uris);
    }
}
