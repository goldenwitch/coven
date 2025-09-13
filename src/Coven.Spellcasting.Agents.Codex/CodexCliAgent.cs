// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using Coven.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Coven.Spellcasting.Spells;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.MCP.Exec;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting.Agents.Codex.Config;

namespace Coven.Spellcasting.Agents.Codex;

    public sealed class CodexCliAgent<TMessageFormat> : ICovenAgent<TMessageFormat> where TMessageFormat : notnull
    {
    public string Id => "codex";
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;
    private readonly IScrivener<TMessageFormat> _scrivener;
    private readonly string _codexHomeDir;
    private readonly ICodexRolloutTranslator<TMessageFormat>? _translator;
    private readonly string? _shimExecutablePath;
    private IMcpSpellExecutorRegistry? _executorRegistry;
    private readonly IMcpServerHost? _hostOverride;
    private readonly ICodexProcessFactory? _procFactory;
    private readonly ITailMuxFactory? _tailFactory;
    private readonly ICodexConfigWriter? _configWriter;
    private readonly IRolloutPathResolver? _rolloutResolver;
    private McpToolbelt? _toolbelt;
        private IMcpServerSession? _mcpSession;
        private readonly ILogger<CodexCliAgent<TMessageFormat>> _log = NullLogger<CodexCliAgent<TMessageFormat>>.Instance;

    // Removed unused process/task tracking fields from an earlier design.

    internal CodexCliAgent(string codexExecutablePath, string workspaceDirectory, IScrivener<TMessageFormat> scrivener, string? shimExecutablePath = null)
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
        
        _log.LogDebug("CodexCliAgent initialized for workspace: {Workspace}", _workspaceDirectory);
    }

    // Constructor that allows specifying a translator for non-string message formats
    internal CodexCliAgent(
        string codexExecutablePath,
        string workspaceDirectory,
        IScrivener<TMessageFormat> scrivener,
        ICodexRolloutTranslator<TMessageFormat> translator,
        string? shimExecutablePath,
        IMcpServerHost? host,
        ICodexProcessFactory? processFactory,
        ITailMuxFactory? tailFactory,
        ICodexConfigWriter? configWriter,
        IRolloutPathResolver? rolloutResolver)
        : this(codexExecutablePath, workspaceDirectory, scrivener, shimExecutablePath)
    {
        _hostOverride = host;
        _procFactory = processFactory;
        _tailFactory = tailFactory;
        _configWriter = configWriter;
        _rolloutResolver = rolloutResolver;
        _translator = translator;
    }

    internal CodexCliAgent(
        string codexExecutablePath,
        string workspaceDirectory,
        IScrivener<TMessageFormat> scrivener,
        string? shimExecutablePath,
        IMcpServerHost? host,
        ICodexProcessFactory? processFactory,
        ITailMuxFactory? tailFactory,
        ICodexConfigWriter? configWriter,
        IRolloutPathResolver? rolloutResolver)
        : this(codexExecutablePath, workspaceDirectory, scrivener, shimExecutablePath)
    {
        _hostOverride = host;
        _procFactory = processFactory;
        _tailFactory = tailFactory;
        _configWriter = configWriter;
        _rolloutResolver = rolloutResolver;
    }

    public Task RegisterSpells(IReadOnlyList<ISpellContract> Spells)
    {
        // Build MCP tools and an executor registry directly from the provided spell instances.
        _toolbelt = McpToolbeltBuilder.FromSpells(Spells);
        _executorRegistry = new ReflectionMcpSpellExecutorRegistry(Spells.Cast<object>());
        return Task.CompletedTask;
    }


    public async Task InvokeAgent(CancellationToken ct = default)
    {
        try
        {
            _log.LogInformation("Starting Codex CLI agent invocation...");
            var env = new Dictionary<string, string?>
            {
                ["CODEX_HOME"] = _codexHomeDir
            };

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
                        try { _configWriter.WriteOrMerge(_codexHomeDir, _shimExecutablePath!, _mcpSession.PipeName!); }
                        catch (Exception ex) { _log.LogWarning(ex, "Failed to write Codex config for shim"); }
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
            _log.LogDebug("Using rollout path: {RolloutPath}", rolloutPath);

            var tailFactory = _tailFactory ?? new DefaultTailMuxFactory();
            await using ITailMux mux = tailFactory.CreateForRollout(rolloutPath, proc);

            // Ingress: begin reading from scrivener and forward user thoughts to Codex stdin.
            // Only supported for ChatEntry journals to avoid echo/feedback loops for plain string journals.
            var ingressTask = Task.CompletedTask;
            if (typeof(TMessageFormat) == typeof(ChatEntry))
            {
                ingressTask = Task.Run(async () =>
                {
                    try
                    {
                        long after = 0;
                        await foreach (var pair in _scrivener.TailAsync(after, ct).ConfigureAwait(false))
                        {
                            ct.ThrowIfCancellationRequested();
                            var entry = (ChatEntry)(object)pair.entry;
                            if (entry is ChatThought thought)
                            {
                                try { await mux.WriteLineAsync(thought.Text, ct).ConfigureAwait(false); }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception ex)
                                {
                                    _log.LogWarning(ex, "Ingress write failed");
                                    await ReportErrorAsync(ex, ct).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { /* normal shutdown */ }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Ingress loop error");
                        await ReportErrorAsync(ex, ct).ConfigureAwait(false);
                    }
                }, ct);
            }

            var rolloutTask = mux.TailAsync(async ev =>
            {
                switch (ev)
                {
                    case Line o:
                    {
                        var parsed = CodexRolloutParser.Parse(o.Line);
                        if (_translator is not null)
                        {
                            var entry = _translator.Translate(parsed);
                            await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
                        }
                        else if (typeof(TMessageFormat) == typeof(string))
                        {
                            await _scrivener.WriteAsync((TMessageFormat)(object)o.Line, ct).ConfigureAwait(false);
                        }
                        break;
                    }

                    case ErrorLine e:
                    {
                        // Surface process/tail errors as error lines to the scrivener via the translator if available
                        var errorLine = new CodexRolloutLine(
                            CodexRolloutLineKind.Error,
                            DateTimeOffset.UtcNow,
                            Raw: e.Line,
                            Message: e.Line,
                            Code: "tail_error");

                        if (_translator is not null)
                        {
                            var entry = _translator.Translate(errorLine);
                            await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
                        }
                        else if (typeof(TMessageFormat) == typeof(string))
                        {
                            await _scrivener.WriteAsync((TMessageFormat)(object)($"ERROR: {e.Line}"), ct).ConfigureAwait(false);
                        }
                        break;
                    }
                }
            }, ct);

            await Task.WhenAll(rolloutTask, ingressTask).ConfigureAwait(false);
            _log.LogInformation("Codex CLI agent invocation completed");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CodexCliAgent.InvokeAgent failed");
            await ReportErrorAsync(ex, ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CloseAgent()
    {
        var s = _mcpSession;
        _mcpSession = null;
        if (s is not null)
        {
            try { await s.DisposeAsync().ConfigureAwait(false); }
            catch
            {
                // best-effort; disposal issues are non-fatal
            }
        }
        return;
    }

    // ---------- helpers ----------
    private async Task ReportErrorAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            _log.LogError(ex, "Agent error: {Message}", ex.Message);
            var line = new CodexRolloutLine(
                CodexRolloutLineKind.Error,
                DateTimeOffset.UtcNow,
                Raw: ex.ToString(),
                Message: ex.Message,
                Code: ex.GetType().Name);

            if (_translator is not null)
            {
                var entry = _translator.Translate(line);
                await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
            }
            else if (typeof(TMessageFormat) == typeof(string))
            {
                var msg = $"ERROR[{ex.GetType().Name}]: {ex.Message}\n{ex}";
                await _scrivener.WriteAsync((TMessageFormat)(object)msg, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            // As a last resort, swallow to avoid masking the original exception
        }
    }
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
