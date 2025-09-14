// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using Coven.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Coven.Spellcasting.Spells;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.MCP.Tools;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting.Agents.Codex.Config;

namespace Coven.Spellcasting.Agents.Codex;

    public sealed class CodexCliAgent<TMessageFormat> : ICovenAgent<TMessageFormat> where TMessageFormat : notnull
    {
    private readonly string _codexExecutablePath;
    private readonly string _workspaceDirectory;
    private readonly IScrivener<TMessageFormat> _scrivener;
    private readonly string _codexHomeDir;
        private readonly ICodexRolloutTranslator<TMessageFormat> _translator;
        private readonly string? _shimExecutablePath;
        private readonly IReadOnlyList<string> _configOverrides;
        private IMcpSpellExecutorRegistry? _executorRegistry;
        private readonly IMcpServerHost? _hostOverride;
        private readonly ITailMuxFactory? _tailFactory;
        private readonly ICodexConfigWriter? _configWriter;
        private McpToolbelt? _toolbelt;
        private IMcpServerSession? _mcpSession;
        private readonly ILogger<CodexCliAgent<TMessageFormat>> _log;

    // Removed unused process/task tracking fields from an earlier design.

    // Constructor that allows specifying a translator for non-string message formats
    internal CodexCliAgent(
        string codexExecutablePath,
        string workspaceDirectory,
        IScrivener<TMessageFormat> scrivener,
        ICodexRolloutTranslator<TMessageFormat> translator,
        string? shimExecutablePath,
        IReadOnlyList<string>? configOverrides,
        IMcpServerHost? host,
        ITailMuxFactory? tailFactory,
        ICodexConfigWriter? configWriter,
        ILogger<CodexCliAgent<TMessageFormat>>? logger = null)
    {
        _codexExecutablePath = codexExecutablePath;
        _workspaceDirectory = workspaceDirectory;
        _scrivener = scrivener;
        _codexHomeDir = Path.Combine(_workspaceDirectory, ".codex");
        _shimExecutablePath = shimExecutablePath;
        try { Directory.CreateDirectory(_codexHomeDir); } catch { }

        _hostOverride = host;
        _tailFactory = tailFactory;
        _configWriter = configWriter;
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _log = logger ?? NullLogger<CodexCliAgent<TMessageFormat>>.Instance;
        _configOverrides = (configOverrides is null) ? Array.Empty<string>() : new List<string>(configOverrides);
        _log.LogDebug("CodexCliAgent initialized for workspace: {Workspace}", _workspaceDirectory);
    }

    

    public Task RegisterSpells(IReadOnlyList<ISpellContract> Spells)
    {
        // Build MCP tools and an executor registry directly from the provided spell instances.
        _toolbelt = McpToolbeltBuilder.FromSpells(Spells);
        _executorRegistry = new SimpleMcpSpellExecutorRegistry(Spells);
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
                _mcpSession = await host.StartAsync(_toolbelt, _executorRegistry, ct).ConfigureAwait(false);
                // Generate a minimal Codex config that points to our shim, which bridges to the named pipe.
                if (!string.IsNullOrWhiteSpace(_shimExecutablePath) && !string.IsNullOrWhiteSpace(_mcpSession.PipeName))
                {
                    var writer = _configWriter ?? new DefaultCodexConfigWriter();
                    try { writer.WriteOrMerge(_codexHomeDir, _shimExecutablePath!, _mcpSession.PipeName!); }
                    catch (Exception ex) { _log.LogWarning(ex, "Failed to write Codex config for shim"); }
                }
                }

            // Use a deterministic session log path under CODEX_HOME/log and enable TUI session recording
            var logDir = Path.Combine(_codexHomeDir, "log");
            try { Directory.CreateDirectory(logDir); } catch { }
            var rolloutPath = Path.Combine(logDir, "codex.rollout.jsonl");
            env["CODEX_TUI_RECORD_SESSION"] = "1";
            env["CODEX_TUI_SESSION_LOG_PATH"] = rolloutPath;
            _log.LogDebug("Using session log path: {RolloutPath}", rolloutPath);

            var tailFactory = _tailFactory ?? new DefaultTailMuxFactory();

            // Build CLI args; avoid redundant flags. Logs default under CODEX_HOME.
            var args = new List<string>();
            // Thread through -c/--config overrides without branching complexity
            foreach (var ov in _configOverrides)
            {
                if (!string.IsNullOrWhiteSpace(ov))
                {
                    args.Add("-c");
                    args.Add(ov);
                }
            }

            await using ITailMux mux = tailFactory.Create(
                documentPath: rolloutPath,
                executablePath: _codexExecutablePath,
                arguments: args,
                workingDirectory: _workspaceDirectory,
                environment: env);

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
                        var entry = _translator.Translate(parsed);
                        await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
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

                        var entry = _translator.Translate(errorLine);
                        await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
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

            var entry = _translator.Translate(line);
            await _scrivener.WriteAsync(entry, ct).ConfigureAwait(false);
        }
        catch
        {
            // As a last resort, swallow to avoid masking the original exception
        }
    }
    

}
