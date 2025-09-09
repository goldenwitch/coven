using System.Diagnostics;
using System.Text.RegularExpressions;
using Coven.Chat;
using Coven.Spellcasting.Spells;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.MCP.Exec;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting.Agents.Codex.Config;

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
    private readonly string? _shimExecutablePath;
    private readonly IMcpSpellExecutorRegistry? _executorRegistry;
    private readonly IMcpServerHost? _hostOverride;
    private readonly ICodexProcessFactory? _procFactory;
    private readonly ITailMuxFactory? _tailFactory;
    private readonly ICodexConfigWriter? _configWriter;
    private readonly IRolloutPathResolver? _rolloutResolver;
    private McpToolbelt? _toolbelt;
    private IMcpServerSession? _mcpSession;

    // Removed unused process/task tracking fields from an earlier design.

    public CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener, string? shimExecutablePath = null, IEnumerable<object>? spells = null)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
        _codexHomeDir = Path.Combine(_workspaceDirectory, ".codex");
        _shimExecutablePath = shimExecutablePath;
        try { Directory.CreateDirectory(_codexHomeDir); } catch { }

        // Default translator for plain strings; callers can extend later via options/DI.
        if (typeof(TMessageFormat) == typeof(string))
        {
            _translator = (ICodexRolloutTranslator<TMessageFormat>) (object) new DefaultStringTranslator();
        }
        
        if (spells is not null)
        {
            // Build an executor registry from the provided spell instances for invocation.
            // Toolbelt (definitions) will be provided via RegisterSpells to ensure the
            // Codex-visible schemas/names come from the caller's Spellbook, not reflection.
            var registry = new ReflectionMcpSpellExecutorRegistry(spells);
            _executorRegistry = registry;
        }
    }

    public CodexCliAgent(
        string codexExecutablePath,
        string workspaceDirectory,
        IScrivener<TMessageFormat> scrivener,
        string? shimExecutablePath,
        IEnumerable<object>? spells,
        IMcpServerHost? host,
        ICodexProcessFactory? processFactory,
        ITailMuxFactory? tailFactory,
        ICodexConfigWriter? configWriter,
        IRolloutPathResolver? rolloutResolver)
        : this(codexExecutablePath, workspaceDirectory, scrivener, shimExecutablePath, spells)
    {
        _hostOverride = host;
        _procFactory = processFactory;
        _tailFactory = tailFactory;
        _configWriter = configWriter;
        _rolloutResolver = rolloutResolver;
    }

    public Task RegisterSpells(List<SpellDefinition> Spells)
    {
        _registeredSpells.Clear();
        _registeredSpells.AddRange(Spells ?? new List<SpellDefinition>());

        // Build MCP tools from caller-provided spell definitions (preferred over reflection).
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

            // If spells were provided (executor registry exists) but no toolbelt was registered,
            // surface a clear configuration error to the developer.
            if ((_executorRegistry is not null) && (_toolbelt is null || _toolbelt.Tools.Count == 0))
            {
                throw new InvalidOperationException(
                    "No MCP tools registered. Call RegisterSpells with your SpellDefinitions to expose tools for the provided spells.");
            }

            // Start disposable MCP server session for this invocation if we have tools.
            if (_toolbelt is not null && _toolbelt.Tools.Count != 0)
            {
                var host = _hostOverride ?? new LocalMcpServerHost(_workspaceDirectory);
                _mcpSession = await (_executorRegistry is null
                    ? host.StartAsync(_toolbelt, ct)
                    : host.StartAsync(_toolbelt, _executorRegistry, ct)).ConfigureAwait(false);
                // Generate a minimal Codex config that points to our shim, which bridges to the named pipe.
                if (!string.IsNullOrWhiteSpace(_shimExecutablePath) && !string.IsNullOrWhiteSpace(_mcpSession.PipeName))
                {
                    if (_configWriter is not null)
                    {
                        try { _configWriter.WriteOrMerge(_codexHomeDir, _shimExecutablePath!, _mcpSession.PipeName!); } catch { }
                    }
                    else
                    {
                        WriteCodexConfigForShim(_shimExecutablePath!, _mcpSession.PipeName!);
                    }
                }
            }

            var processFactory = _procFactory ?? new DefaultCodexProcessFactory();
            await using var handle = processFactory.Start(_codexExecutablePath, _workspaceDirectory, env);
            var proc = handle.Process;

            // Determine rollout path for the just-started session
            var resolver = _rolloutResolver ?? new DefaultRolloutPathResolver();
            var rolloutPath = await resolver.ResolveAsync(_codexExecutablePath, _workspaceDirectory, _codexHomeDir, env, TimeSpan.FromSeconds(8), ct)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(rolloutPath))
            {
                // Fallback to a local log; mux will wait if/when it appears
                rolloutPath = Path.Combine(_workspaceDirectory, "codex.rollout.jsonl");
            }

            var tailFactory = _tailFactory ?? new DefaultTailMuxFactory();
            await using ITailMux mux = tailFactory.CreateForRollout(rolloutPath, proc);

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
    private void WriteCodexConfigForShim(string shimPath, string pipeName)
    {
        try
        {
            Directory.CreateDirectory(_codexHomeDir);
            var cfgPath = Path.Combine(_codexHomeDir, "config.toml");
            var toml = $"[mcp_servers.coven]\ncommand = \"{EscapeToml(shimPath)}\"\nargs = [\"{EscapeToml(pipeName)}\"]\n";
            if (File.Exists(cfgPath))
            {
                var existing = File.ReadAllText(cfgPath);
                var merged = MergeToml(existing, toml, "[mcp_servers.coven]");
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
            // Fallback: append
            if (!existing.EndsWith("\n") && !existing.EndsWith("\r\n")) existing += Environment.NewLine;
            return existing + newSection;
        }
    }

}
