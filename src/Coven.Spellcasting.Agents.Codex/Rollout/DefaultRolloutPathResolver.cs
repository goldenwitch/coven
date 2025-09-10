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
            var viaCli = TryResolveRolloutPathViaCli(codexExecutablePath, workspaceDirectory, env);
            if (!string.IsNullOrWhiteSpace(viaCli) && File.Exists(viaCli))
                return viaCli;

            var viaScan = TryResolveRolloutPathByScan(codexHomeDir);
            if (!string.IsNullOrWhiteSpace(viaScan) && File.Exists(viaScan))
                return viaScan;

            try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { break; }
        }

        return null;
    }

    private static string? TryResolveRolloutPathByScan(string codexHomeDir)
    {
        try
        {
            var sessionsRoot = Path.Combine(codexHomeDir, "sessions");
            if (!Directory.Exists(sessionsRoot)) return null;
            return Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories)
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    private static string? TryResolveRolloutPathViaCli(
        string codexExecutablePath,
        string workspaceDirectory,
        IReadOnlyDictionary<string, string?> env)
    {
        foreach (var args in new[] { "sessions list", "session ls" })
        {
            try
            {
                // Try direct
                var output = TryRunCapture(codexExecutablePath, args, workspaceDirectory, env);
                // Windows fallback via cmd.exe for .cmd shims
                if (output is null && OperatingSystem.IsWindows())
                {
                    var cmdArgs = "/c " + BuildCommandLine(codexExecutablePath, args);
                    output = TryRunCapture("cmd.exe", cmdArgs, workspaceDirectory, env);
                }

                // npm discovery fallback
                if (output is null)
                {
                    var npmCodex = ExecutableDiscovery.TryLocateCodexViaNpm();
                    if (!string.IsNullOrWhiteSpace(npmCodex))
                    {
                        output = TryRunCapture(npmCodex!, args, workspaceDirectory, env);
                        if (output is null && OperatingSystem.IsWindows())
                        {
                            var cmdArgs2 = "/c " + BuildCommandLine(npmCodex!, args);
                            output = TryRunCapture("cmd.exe", cmdArgs2, workspaceDirectory, env);
                        }
                    }
                }

                // Windows PATH discovery via 'where' (prefer .cmd)
                if (output is null)
                {
                    var winCodex = ExecutableDiscovery.TryLocateCodexOnWindowsPath();
                    if (!string.IsNullOrWhiteSpace(winCodex))
                    {
                        output = TryRunCapture(winCodex!, args, workspaceDirectory, env);
                        if (output is null && OperatingSystem.IsWindows())
                        {
                            var cmdArgs3 = "/c " + BuildCommandLine(winCodex!, args);
                            output = TryRunCapture("cmd.exe", cmdArgs3, workspaceDirectory, env);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(output)) continue;

                var rx = new Regex(@"(?<path>[^\s]+rollout-[^\s]+\.jsonl)", RegexOptions.IgnoreCase);
                var match = rx.Matches(output).Cast<Match>().Select(m => m.Groups["path"].Value).LastOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    var path = ExpandUserHome(match);
                    return Path.GetFullPath(path);
                }
            }
            catch { }
        }
        return null;
    }

    private static string? TryRunCapture(
        string fileName,
        string? arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> env)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments ?? string.Empty)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true
            };
            foreach (var kv in env)
            {
                if (kv.Value is null) psi.Environment.Remove(kv.Key);
                else psi.Environment[kv.Key] = kv.Value;
            }
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnv))
                psi.Environment["PATH"] = pathEnv;

            using var p = Process.Start(psi);
            if (p is null) return null;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return p.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    private static string BuildCommandLine(string fileName, string? arguments)
    {
        var needsQuotes = fileName.Contains(' ') && !fileName.StartsWith('"') && !fileName.EndsWith('"');
        var quoted = needsQuotes ? $"\"{fileName}\"" : fileName;
        return string.IsNullOrWhiteSpace(arguments) ? quoted : $"{quoted} {arguments}";
    }

    private static string ExpandUserHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var rest = path.TrimStart('~').TrimStart('/', '\\');
            return Path.Combine(home, rest);
        }
        return path;
    }
}
