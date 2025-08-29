namespace Coven.Spellcasting.Agents.Codex;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Validation;

/// <summary>
/// Idempotent validation for the Codex CLI.
/// - Probes readiness via an overridable probe (defaults to executing "codex --version").
/// - Allows optional installer delegate for teams to plug in their bootstrap steps.
/// </summary>
public sealed class CodexCliValidation : IdempotentAgentValidation
{
    public sealed class Options
    {
        public string ExecutablePath { get; init; } = "codex";
        public string? MinVersion { get; init; }
        public Func<SpellContext?, CancellationToken, Task<bool>>? ProbeAsync { get; init; }
        public Func<SpellContext?, CancellationToken, Task>? InstallerAsync { get; init; }
        public string? StampDirectory { get; init; }
    }

    private readonly Options _opts;

    public CodexCliValidation(Options? options = null) : base("codex")
    {
        _opts = options ?? new Options();
    }

    protected override string ComputeSpec(SpellContext? context)
        => $"{AgentId}|exe={_opts.ExecutablePath}|min={_opts.MinVersion ?? ""}";

    protected override string GetStampDirectory(SpellContext? context)
    {
        if (!string.IsNullOrEmpty(_opts.StampDirectory))
            return _opts.StampDirectory!;

        // Prefer sidecar .codex dir under working directory if context provides one.
        if (context?.ContextUri is { IsAbsoluteUri: true, Scheme: "file" } uri)
        {
            var baseDir = Path.GetFullPath(uri.LocalPath);
            var codex = Path.Combine(baseDir, ".codex");
            return IsTestEnvironment() ? Path.Combine(codex, "tests") : codex;
        }

        // Fallback to ~/.codex (or Windows profile equivalent)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codexHome = Path.Combine(home, ".codex");
        return IsTestEnvironment() ? Path.Combine(codexHome, "tests") : codexHome;
    }

    private static bool IsTestEnvironment()
    {
        var v = Environment.GetEnvironmentVariable("COVEN_ENV");
        if (!string.IsNullOrEmpty(v) && string.Equals(v, "test", StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            var name = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
            if (name.Contains("testhost") || name.Contains("vstest") || name.Contains("xunit"))
                return true;
        }
        catch { }
        return false;
    }

    protected override async Task<bool> IsAlreadyReadyAsync(SpellContext? context, CancellationToken ct)
    {
        if (_opts.ProbeAsync is not null)
            return await _opts.ProbeAsync(context, ct).ConfigureAwait(false);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _opts.ExecutablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            if (context?.ContextUri is { IsAbsoluteUri: true, Scheme: "file" } uri)
                psi.WorkingDirectory = Path.GetFullPath(uri.LocalPath);

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            var text = output.Length > 0 ? output : err;

            if (string.IsNullOrWhiteSpace(text)) return false;
            if (string.IsNullOrWhiteSpace(_opts.MinVersion)) return true;

            // Very loose check: ensure reported version contains MinVersion substring
            return text.Contains(_opts.MinVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    protected override async Task ProvisionAsync(SpellContext? context, CancellationToken ct)
    {
        if (_opts.InstallerAsync is not null)
        {
            await _opts.InstallerAsync(context, ct).ConfigureAwait(false);
            return;
        }
        // Default: no-op; teams provide InstallerAsync to perform installation.
    }
}
