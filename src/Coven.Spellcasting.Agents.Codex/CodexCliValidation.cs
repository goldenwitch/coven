// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using System.IO.Pipes;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents.Validation;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.Validation;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliValidation : IAgentValidation
{
    public string AgentId => "Codex";

    private readonly string _executablePath;
    private readonly string _workspaceDirectory;
    private readonly string? _shimExecutablePath;
    private readonly Spellbook? _spellbook;
    private readonly ICodexConfigWriter _configWriter;
    private readonly IValidationOps _ops;

    internal CodexCliValidation(
        string executablePath,
        string workspaceDirectory,
        string? shimExecutablePath,
        Spellbook? spellbook,
        ICodexConfigWriter configWriter,
        IValidationOps? ops = null)
    {
        _executablePath = string.IsNullOrWhiteSpace(executablePath) ? "codex" : executablePath;
        _workspaceDirectory = string.IsNullOrWhiteSpace(workspaceDirectory) ? Directory.GetCurrentDirectory() : workspaceDirectory;
        _shimExecutablePath = shimExecutablePath;
        _spellbook = spellbook;
        _configWriter = configWriter ?? throw new ArgumentNullException(nameof(configWriter));
        _ops = ops ?? new DefaultValidationOps();
    }

    public async Task<AgentValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var notes = new List<string>();
        bool performed = false;

        bool shimRequired = _spellbook?.Spells?.Count > 0;
        var plannerInputs = new CodexValidationPlanner.Inputs(
            _executablePath,
            _workspaceDirectory,
            _shimExecutablePath,
            shimRequired);
        var plan = CodexValidationPlanner.Create(plannerInputs);

        // 1) Version probe
        EnsureNotCancelled(ct);
        string codexExec = plan.VersionProbe.FileName;
        var ver = _ops.RunProcess(codexExec, plan.VersionProbe.Arguments, plan.VersionProbe.WorkingDirectory, plan.VersionProbe.Environment);
        if (!ver.Ok)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            throw new InvalidOperationException($"Codex CLI not found or not runnable. Looked for '{_executablePath}'. PATH begins: '" + Truncate(pathEnv, 2000) + "'. Set ExecutablePath to an absolute path or ensure it is on PATH.");
        }
        notes.Add(!string.IsNullOrWhiteSpace(ver.StdOut) ? $"codex ok: {ver.StdOut.Trim()}" : "codex ok: version probe succeeded");

        // 2) Workspace ensure + write probe
        EnsureNotCancelled(ct);
        if (!Directory.Exists(plan.WorkspaceEnsure.Path)) { _ops.EnsureDirectory(plan.WorkspaceEnsure.Path); performed = true; notes.Add($"workspace created: {plan.WorkspaceEnsure.Path}"); }
        try { await _ops.WriteFileAsync(plan.WorkspaceWriteProbe.Path, plan.WorkspaceWriteProbe.Contents, ct).ConfigureAwait(false); if (plan.WorkspaceWriteProbe.DeleteAfter) _ops.DeleteFile(plan.WorkspaceWriteProbe.Path); notes.Add("workspace writable: ok"); }
        catch { throw new InvalidOperationException($"Workspace '{plan.WorkspaceEnsure.Path}' is not writable."); }

        // 3) Codex home ensure + write probe
        EnsureNotCancelled(ct);
        if (!Directory.Exists(plan.CodexHomeEnsure.Path)) { _ops.EnsureDirectory(plan.CodexHomeEnsure.Path); performed = true; notes.Add($"codex home created: {plan.CodexHomeEnsure.Path}"); }
        try { await _ops.WriteFileAsync(plan.CodexHomeWriteProbe.Path, plan.CodexHomeWriteProbe.Contents, ct).ConfigureAwait(false); if (plan.CodexHomeWriteProbe.DeleteAfter) _ops.DeleteFile(plan.CodexHomeWriteProbe.Path); notes.Add("codex home writable: ok"); }
        catch { throw new InvalidOperationException($"Codex home '{plan.CodexHomeEnsure.Path}' is not writable."); }

        // 4) Pipes handshake
        EnsureNotCancelled(ct);
        try
        {
            _ops.PipeHandshake(plan.PipeProbe.PipeName, ct);
            notes.Add("named pipes: ok");
        }
        catch { throw new InvalidOperationException("Named pipes are unavailable; MCP shim cannot connect."); }

        // 5) Shim probe (if planned)
        EnsureNotCancelled(ct);
        if (shimRequired)
        {
            if (string.IsNullOrWhiteSpace(plan.ShimPath) || !_ops.FileExists(plan.ShimPath))
                throw new InvalidOperationException("MCP shim not found. Build output should include mcp-shim/ with the shim executable.");
            if (plan.ShimHelpProbe is not null)
            {
                var shimRun = _ops.RunProcess(plan.ShimHelpProbe.FileName, plan.ShimHelpProbe.Arguments, plan.ShimHelpProbe.WorkingDirectory, plan.ShimHelpProbe.Environment);
                if (!shimRun.Ok) throw new InvalidOperationException("MCP shim is not runnable.");
            }
            notes.Add($"shim ok: {plan.ShimPath}");
        }

        // 6) Config merge
        EnsureNotCancelled(ct);
        try
        {
            _ops.MergeConfig(_configWriter, plan.ConfigMerge.CodexHomeDir, plan.ConfigMerge.ShimPath, plan.ConfigMerge.PipeName, plan.ConfigMerge.ServerKey);
            notes.Add("codex config merge: ok");
        }
        catch { throw new InvalidOperationException("Failed to write or merge Codex config.toml."); }

        // 7) Sessions probe
        EnsureNotCancelled(ct);
        _ = _ops.RunProcess(codexExec, plan.SessionsListProbe.Arguments, plan.SessionsListProbe.WorkingDirectory, plan.SessionsListProbe.Environment);
        notes.Add("codex sessions probe: attempted");

        var msg = string.Join("; ", notes);
        return performed ? AgentValidationResult.Performed(msg) : AgentValidationResult.Noop(msg);
    }

    private static void EnsureNotCancelled(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
    }

    // Note: process execution is abstracted via IValidationOps; no direct process calls here.

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "...";

    // Shim discovery logic consolidated in DI; no duplicate here
}
