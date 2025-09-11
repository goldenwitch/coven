// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents.Codex.Config;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ConfigWriterTests
{
    [Fact]
    public void WriteOrMerge_Writes_New_File_And_Is_Idempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"coven_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var writer = new DefaultCodexConfigWriter();
            var cfg = Path.Combine(root, "config.toml");

            writer.WriteOrMerge(root, shimPath: "C:/shim.exe", pipeName: "pipe_abc", serverKey: "coven");
            Assert.True(File.Exists(cfg));
            var first = File.ReadAllText(cfg);

            // Should contain exactly one [mcp_servers.coven] section and our values
            Assert.Contains("[mcp_servers.coven]", first);
            Assert.Contains("command = \"C:/shim.exe\"", first);
            Assert.Contains("args = [\"pipe_abc\"]", first);
            Assert.Equal(1, CountOccurrences(first, "[mcp_servers.coven]"));

            // Second call replaces the same section without duplicating
            writer.WriteOrMerge(root, shimPath: "C:/shim.exe", pipeName: "pipe_abc", serverKey: "coven");
            var second = File.ReadAllText(cfg);
            Assert.Equal(1, CountOccurrences(second, "[mcp_servers.coven]"));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void WriteOrMerge_Replaces_Existing_Section()
    {
        var root = Path.Combine(Path.GetTempPath(), $"coven_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var cfg = Path.Combine(root, "config.toml");
            File.WriteAllText(cfg,
                "[mcp_servers.coven]\ncommand = \"old\"\nargs = [\"oldpipe\"]\n\n[other]\nkey=1\n");

            var writer = new DefaultCodexConfigWriter();
            writer.WriteOrMerge(root, shimPath: "/opt/shim", pipeName: "newpipe", serverKey: "coven");
            var text = File.ReadAllText(cfg);

            Assert.Contains("[mcp_servers.coven]", text);
            Assert.DoesNotContain("oldpipe", text);
            Assert.Contains("newpipe", text);
            Assert.Contains("[other]", text); // ensure other sections preserved
            Assert.Equal(1, CountOccurrences(text, "[mcp_servers.coven]"));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(value, idx, StringComparison.Ordinal)) >= 0) { count++; idx += value.Length; }
        return count;
    }
}
