// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Validation;

// Pure, testable planning for Codex validation. Produces specs with no side effects.
internal static class CodexValidationPlanner
{
    public sealed record Inputs(string ExecutablePath, string WorkspaceDirectory, string? ShimExecutablePath, bool ShimRequired);

    public sealed record EnsureDirectorySpec(string Path);
    public sealed record FileWriteProbeSpec(string Path, string Contents, bool DeleteAfter = true);
    public sealed record PipeHandshakeSpec(string PipeName);
    public sealed record ProcessSpec(string FileName, string Arguments, string WorkingDirectory, IReadOnlyDictionary<string, string?> Environment);
    public sealed record ConfigMergeSpec(string CodexHomeDir, string ShimPath, string PipeName, string ServerKey);

    public sealed record Plan(
        EnsureDirectorySpec WorkspaceEnsure,
        FileWriteProbeSpec WorkspaceWriteProbe,
        EnsureDirectorySpec CodexHomeEnsure,
        FileWriteProbeSpec CodexHomeWriteProbe,
        ProcessSpec VersionProbe,
        PipeHandshakeSpec PipeProbe,
        ProcessSpec? ShimHelpProbe,
        ConfigMergeSpec ConfigMerge,
        ProcessSpec SessionsListProbe,
        string CodexHomeDir,
        string? ShimPath
    );

    public static Plan Create(Inputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));

        var codexHome = Path.Combine(inputs.WorkspaceDirectory, ".codex");
        var env = new Dictionary<string, string?> { ["CODEX_HOME"] = codexHome };

        var version = new ProcessSpec(inputs.ExecutablePath, "--version", inputs.WorkspaceDirectory, env);
        var sessions = new ProcessSpec(inputs.ExecutablePath, "sessions list", inputs.WorkspaceDirectory, env);

        // Named pipe name for handshake + config
        var pipe = $"coven_probe_{Guid.NewGuid():N}";

        ProcessSpec? shimProbe = null;
        if (inputs.ShimRequired && inputs.ShimExecutablePath is not null)
        {
            shimProbe = new ProcessSpec(inputs.ShimExecutablePath, "--help", inputs.WorkspaceDirectory, new Dictionary<string, string?>());
        }

        var p = new Plan(
            WorkspaceEnsure: new EnsureDirectorySpec(inputs.WorkspaceDirectory),
            WorkspaceWriteProbe: new FileWriteProbeSpec(Path.Combine(inputs.WorkspaceDirectory, $".coven_probe_{Guid.NewGuid():N}"), "ok", true),
            CodexHomeEnsure: new EnsureDirectorySpec(codexHome),
            CodexHomeWriteProbe: new FileWriteProbeSpec(Path.Combine(codexHome, $"probe_{Guid.NewGuid():N}.tmp"), "ok", true),
            VersionProbe: version,
            PipeProbe: new PipeHandshakeSpec(pipe),
            ShimHelpProbe: shimProbe,
            ConfigMerge: new ConfigMergeSpec(codexHome, inputs.ShimExecutablePath ?? "shim-not-required", pipe, "coven"),
            SessionsListProbe: sessions,
            CodexHomeDir: codexHome,
            ShimPath: inputs.ShimExecutablePath
        );

        return p;
    }
}
