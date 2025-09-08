namespace Coven.Spellcasting.Agents.Codex.Config;

internal sealed class DefaultCodexConfigWriter : ICodexConfigWriter
{
    public void WriteOrMerge(string codexHomeDir, string shimPath, string pipeName, string serverKey = "coven")
    {
        try
        {
            Directory.CreateDirectory(codexHomeDir);
            var cfgPath = Path.Combine(codexHomeDir, "config.toml");
            var header = $"[mcp_servers.{EscapeToml(serverKey)}]";
            var toml = $"{header}\ncommand = \"{EscapeToml(shimPath)}\"\nargs = [\"{EscapeToml(pipeName)}\"]\n";
            if (File.Exists(cfgPath))
            {
                var existing = File.ReadAllText(cfgPath);
                var merged = MergeToml(existing, toml, header);
                File.WriteAllText(cfgPath, merged);
            }
            else
            {
                File.WriteAllText(cfgPath, toml);
            }
        }
        catch { }
    }

    private static string EscapeToml(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string MergeToml(string existing, string newSection, string sectionHeader)
    {
        try
        {
            var lines = existing.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            int start = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                    break;
                }
            }
            if (start >= 0)
            {
                int end = lines.Count;
                for (int i = start + 1; i < lines.Count; i++)
                {
                    var t = lines[i].TrimStart();
                    if (t.StartsWith("[")) { end = i; break; }
                }
                lines.RemoveRange(start, end - start);
                var newLines = newSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                lines.InsertRange(start, newLines);
                return string.Join(Environment.NewLine, lines);
            }
            else
            {
                if (!existing.EndsWith("\n") && !existing.EndsWith("\r\n")) existing += Environment.NewLine;
                return existing + newSection;
            }
        }
        catch
        {
            if (!existing.EndsWith("\n") && !existing.EndsWith("\r\n")) existing += Environment.NewLine;
            return existing + newSection;
        }
    }
}

