// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Diagnostics;
using System.IO;

namespace Coven.Spellcasting.Agents.Codex;

/// <summary>
/// Extensions for configuring process launch for Codex CLI.
/// </summary>
public static class ProcessStartInfoExtensions
{
    /// <summary>
    /// Augments the PATH to include likely Codex locations, notably the global npm bin on Windows.
    /// Adds entries only if they exist on disk and are not already present (case-insensitive on Windows).
    /// </summary>
    public static void AugmentPathForCodex(this ProcessStartInfo psi)
    {
        if (psi is null) throw new ArgumentNullException(nameof(psi));

        // Determine candidate locations
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            // Common global npm bin on Windows: %APPDATA%\npm
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                candidates.Add(Path.Combine(appData, "npm"));
            }
        }
        else
        {
            // Minimal hints for non-Windows (best-effort; do not expand scope):
            // - /usr/local/bin (Homebrew/macOS common)
            // - ~/.npm-global/bin (common when user configures npm prefix)
            candidates.Add("/usr/local/bin");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                candidates.Add(Path.Combine(home, ".npm-global", "bin"));
            }
        }

        // Build normalized current PATH
        var pathVarName = OperatingSystem.IsWindows() ? "Path" : "PATH";
        psi.Environment.TryGetValue(pathVarName, out var existingPath);
        var basePath = existingPath ?? Environment.GetEnvironmentVariable(pathVarName) ?? string.Empty;

        // Path separator is platform-specific
        var sep = Path.PathSeparator;

        // Split and normalize existing entries for lookup
        var existingEntries = new HashSet<string>(
            (basePath.Length > 0 ? basePath.Split(sep) : Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath),
            StringComparer.OrdinalIgnoreCase);

        // Filter candidates: must exist and not already present
        var toPrepend = new List<string>();
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            var norm = NormalizePath(c);
            if (!existingEntries.Contains(norm) && Directory.Exists(c))
            {
                toPrepend.Add(c);
                existingEntries.Add(norm);
            }
        }

        if (toPrepend.Count == 0) return;

        // Prepend new entries so they take effect
        var newPath = string.Join(sep, toPrepend) + (basePath.Length > 0 ? sep + basePath : string.Empty);
        psi.Environment[pathVarName] = newPath;
    }

    private static string NormalizePath(string p)
    {
        try { return Path.GetFullPath(p.Trim()); }
        catch { return p.Trim(); }
    }
}

