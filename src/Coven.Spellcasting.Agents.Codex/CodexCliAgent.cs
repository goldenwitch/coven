using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using Coven.Chat;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliAgent<TMessageFormat> : ICovenAgent<TMessageFormat> where TMessageFormat : notnull
{
    public string Id => "codex";
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;
    private readonly List<SpellDefinition> _registeredSpells = new();
    private readonly IScrivener<TMessageFormat> _scrivener;
    private readonly string _codexHomeDir;

    // Removed unused process/task tracking fields from an earlier design.

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
        _codexHomeDir = Path.Combine(_workspaceDirectory, ".codex");
        try { Directory.CreateDirectory(_codexHomeDir); } catch { }
    }

    public Task RegisterSpells(List<SpellDefinition> Spells)
    {
        _registeredSpells.Clear();
        _registeredSpells.AddRange(Spells ?? new List<SpellDefinition>());

        // Build MCP tools for each spell.

        return Task.CompletedTask;
    }


    public async Task InvokeAgent(CancellationToken ct = default)
    {
        try
        {
            var env = new Dictionary<string, string?>
            {
                ["CODEX_HOME"] = _codexHomeDir
            };

            string? cachedRolloutPath = null;
            string? ResolveRolloutPath()
            {
                if (!string.IsNullOrWhiteSpace(cachedRolloutPath) && File.Exists(cachedRolloutPath))
                    return cachedRolloutPath;

                // Try CLI introspection first (Option #2)
                cachedRolloutPath = TryResolveRolloutPathViaCli(_codexExecutablePath, _workspaceDirectory, env);
                if (!string.IsNullOrWhiteSpace(cachedRolloutPath) && File.Exists(cachedRolloutPath))
                    return cachedRolloutPath;

                // Fallback: scan filesystem under CODEX_HOME
                cachedRolloutPath = TryResolveRolloutPathByScan(_codexHomeDir);
                return cachedRolloutPath;
            }

            await using ITailMux mux = new ProcessDocumentTailMux(
                documentPathResolver: ResolveRolloutPath,
                fileName: _codexExecutablePath,
                arguments: null,
                workingDirectory: _workspaceDirectory,
                environment: env,
                configurePsi: psi =>
                {
                    psi.CreateNoWindow = true;
                });

            await mux.TailAsync(async ev =>
            {
                switch (ev)
                {
                    case Line o:
                        // Forward rollout log lines to journal if desired.
                        // await _scrivener.WriteAsync((TMessageFormat)(object)o.Line, ct);
                        // No prompt detection from rollout logs; sending is driven externally.
                        break;

                    case ErrorLine e:
                        // _ = _scrivener.Error(e.Line);
                        break;
                }
            }, ct);
        }
        catch
        {

        }
    }

    public Task CloseAgent()
    {
        throw new NotImplementedException();
    }

    public Task<TMessageFormat> ReadMessage()
    {
        throw new NotImplementedException();
    }

    public Task SendMessage(TMessageFormat message)
    {
        throw new NotImplementedException();
    }

    // ---------- helpers ----------
    private static string? TryResolveRolloutPathViaCli(
        string codexExecutablePath,
        string workspaceDirectory,
        IReadOnlyDictionary<string, string?> env)
    {
        foreach (var args in new[] { "sessions list", "session ls" })
        {
            try
            {
                var psi = new ProcessStartInfo(codexExecutablePath, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = workspaceDirectory
                };
                foreach (var kv in env)
                {
                    if (kv.Value is null) psi.Environment.Remove(kv.Key);
                    else psi.Environment[kv.Key] = kv.Value;
                }

                using var p = Process.Start(psi);
                if (p is null) continue;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                // Look for a path ending with rollout-*.jsonl
                // Simple regex tolerant of spaces and tildes.
                var rx = new Regex(@"(?<path>[^\s]+rollout-[^\s]+\.jsonl)", RegexOptions.IgnoreCase);
                var match = rx.Matches(output).Cast<Match>().Select(m => m.Groups["path"].Value).LastOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    var path = match;
                    // Expand ~ if present
                    if (path.StartsWith("~"))
                    {
                        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        path = Path.Combine(home, path.TrimStart('~').TrimStart('/', '\\'));
                    }
                    // Normalize
                    path = Path.GetFullPath(path);
                    return path;
                }
            }
            catch { }
        }

        return null;
    }

    private static string? TryResolveRolloutPathByScan(string codexHomeDir)
    {
        try
        {
            var sessionsRoot = Path.Combine(codexHomeDir, "sessions");
            if (!Directory.Exists(sessionsRoot)) return null;

            string? latest = null;
            DateTime latestTime = DateTime.MinValue;
            foreach (var file in Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                DateTime t;
                try { t = File.GetLastWriteTimeUtc(file); }
                catch { continue; }
                if (t > latestTime)
                {
                    latestTime = t;
                    latest = file;
                }
            }
            return latest;
        }
        catch { return null; }
    }

}
