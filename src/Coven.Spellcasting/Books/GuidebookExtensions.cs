// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

public static class GuidebookExtensions
{
    public static IEnumerable<(string Title, string Body)> EnumerateSections(this Guidebook guidebook)
    {
        foreach (var kvp in guidebook.Sections)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }

    public static string? GetSection(this Guidebook guidebook, string title)
    {
        if (guidebook.Sections.TryGetValue(title, out var body)) return body;
        foreach (var kv in guidebook.Sections)
        {
            if (string.Equals(kv.Key, title, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        }
        return null;
    }

    public static bool TryGetSection(this Guidebook guidebook, string title, out string body)
    {
        var v = guidebook.GetSection(title);
        if (v is not null) { body = v; return true; }
        body = string.Empty;
        return false;
    }

    public static string BuildInstructions(this Guidebook guidebook, bool includeTitles = true)
    {
        var parts = new List<string>();
        foreach (var (title, body) in guidebook.EnumerateSections())
        {
            if (includeTitles)
            {
                parts.Add(string.IsNullOrWhiteSpace(body) ? $"## {title}" : $"## {title}\n{body}");
            }
            else
            {
                parts.Add(body);
            }
        }
        return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
    }
}