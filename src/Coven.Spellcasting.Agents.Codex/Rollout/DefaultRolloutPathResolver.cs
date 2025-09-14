// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Coven.Spellcasting.Agents.Codex.Rollout;

internal sealed class DefaultRolloutPathResolver : IRolloutPathResolver
{
    public async Task<string?> ResolveAsync(
        string codexExecutablePath,
        string workspaceDirectory,
        string codexHomeDir,
        IReadOnlyDictionary<string, string?> env,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var viaScan = TryResolveRolloutPathByScan(codexHomeDir);
            if (!string.IsNullOrWhiteSpace(viaScan) && File.Exists(viaScan))
                return viaScan;

            try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { break; }
        }

        return null;
    }

    private static string? TryResolveRolloutPathByScan(string logsRoot)
    {
        try
        {
            if (!Directory.Exists(logsRoot)) return null;
            return SafeEnumerateFiles(logsRoot, "rollout-*.jsonl")
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
    {
        try { return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }
    
}
