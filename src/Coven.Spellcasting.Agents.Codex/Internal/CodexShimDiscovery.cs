// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Internal;

internal static class CodexShimDiscovery
{
    // Restrictive discovery: only return known shim filenames. Do not fall back to arbitrary files.
    public static string? Discover(string? provided)
    {
        try
        {
            // If caller provided a path explicitly, return it as-is. Existence is validated by CodexCliValidation when required.
            if (!string.IsNullOrWhiteSpace(provided)) return provided;

            var baseDir = AppContext.BaseDirectory;
            var shimDir = Path.Combine(baseDir, "mcp-shim");
            if (!Directory.Exists(shimDir)) return null;

            var exe = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.exe");
            if (File.Exists(exe)) return exe;
            var dll = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.dll");
            if (File.Exists(dll)) return dll;
        }
        catch { }

        return null;
    }
}
