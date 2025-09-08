using System.Diagnostics;
using System.Text.RegularExpressions;
using Coven.Chat;
using Coven.Spellcasting.Spells;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.MCP;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliAgent<TMessageFormat> : ICovenAgent<TMessageFormat> where TMessageFormat : notnull
{
    public string Id => "codex";
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;
    private readonly List<SpellDefinition> _registeredSpells = new();
    private readonly IScrivener<TMessageFormat> _scrivener;
    private readonly string _codexHomeDir;
    private readonly ICodexRolloutTranslator<TMessageFormat>? _translator;
    private McpToolbelt? _toolbelt;
    private IMcpServerSession? _mcpSession;

    // Removed unused process/task tracking fields from an earlier design.

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
        _codexHomeDir = Path.Combine(_workspaceDirectory, ".codex");
        try { Directory.CreateDirectory(_codexHomeDir); } catch { }

        // Default translator for plain strings; callers can extend later via options/DI.
        if (typeof(TMessageFormat) == typeof(string))
        {
            _translator = (ICodexRolloutTranslator<TMessageFormat>) (object) new DefaultStringTranslator();
        }
    }

    public Task RegisterSpells(List<SpellDefinition> Spells)
    {
        _registeredSpells.Clear();
        _registeredSpells.AddRange(Spells ?? new List<SpellDefinition>());

        // Build MCP tools for each spell.
        _toolbelt = McpToolbeltBuilder.FromSpells(_registeredSpells);
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

            // Start disposable MCP server session for this invocation if we have tools.
            if (_toolbelt is not null && _toolbelt.Tools.Count != 0)
            {
                var host = new LocalMcpServerHost(_workspaceDirectory);
                _mcpSession = await host.StartAsync(_toolbelt, ct).ConfigureAwait(false);
                foreach (var kv in _mcpSession.EnvironmentOverrides)
                {
                    env[kv.Key] = kv.Value;
                }
            }

            // Start Codex CLI process (owned by mux later)
            var psi = new ProcessStartInfo(_codexExecutablePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = _workspaceDirectory,
                CreateNoWindow = true
            };
            foreach (var kv in env)
            {
                if (kv.Value is null) psi.Environment.Remove(kv.Key);
                else psi.Environment[kv.Key] = kv.Value;
            }

            using var proc = new Process { StartInfo = psi };
            if (!proc.Start()) throw new InvalidOperationException("Failed to start Codex CLI process.");

            // Determine rollout path for the just-started session
            var rolloutPath = await ResolveRolloutPathAsync(_codexExecutablePath, _workspaceDirectory, _codexHomeDir, env, TimeSpan.FromSeconds(8), ct)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(rolloutPath))
            {
                // Fallback to a local log; mux will wait if/when it appears
                rolloutPath = Path.Combine(_workspaceDirectory, "codex.rollout.jsonl");
            }

            await using ITailMux mux = new ProcessDocumentTailMux(rolloutPath, proc);

            await mux.TailAsync(async ev =>
            {
                switch (ev)
                {
                    case Line o:
                        var parsed = CodexRolloutParser.Parse(o.Line);
                        if (_translator is not null)
                        {
                            var entry = _translator.Translate(parsed);
                            try { await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false); } catch { }
                        }
                        else if (_translator is null && typeof(TMessageFormat) == typeof(string))
                        {
                            try { await _scrivener.WriteAsync((TMessageFormat)(object)o.Line, ct).ConfigureAwait(false); } catch { }
                        }
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

    public async Task CloseAgent()
    {
        var s = _mcpSession;
        _mcpSession = null;
        if (s is not null)
        {
            try { await s.DisposeAsync().ConfigureAwait(false); } catch { }
        }
        return;
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
    private static async Task<string?> ResolveRolloutPathAsync(
        string codexExecutablePath,
        string workspaceDirectory,
        string codexHome,
        IReadOnlyDictionary<string, string?> env,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        // Prefer CLI introspection, retrying until timeout
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var viaCli = TryResolveRolloutPathViaCli(codexExecutablePath, workspaceDirectory, env);
            if (!string.IsNullOrWhiteSpace(viaCli) && File.Exists(viaCli))
                return viaCli;

            // Fallback: scan sessions tree for the newest rollout file
            var viaScan = TryResolveRolloutPathByScan(codexHome);
            if (!string.IsNullOrWhiteSpace(viaScan) && File.Exists(viaScan))
                return viaScan;

            try { await Task.Delay(200, ct).ConfigureAwait(false); } catch { break; }
        }

        return null;
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

                // Extract last occurrence of a rollout-*.jsonl path
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
