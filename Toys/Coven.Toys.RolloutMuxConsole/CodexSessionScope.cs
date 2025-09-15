// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Toys.RolloutMuxConsole;

/// <summary>
/// Manages Codex session directories under the workspace and cleans up
/// files/directories that were created by this scope when disposed.
/// </summary>
internal sealed class CodexSessionScope : IAsyncDisposable
{
    public string Workspace { get; }
    public string CodexHome { get; }
    public string LogDir { get; }
    public string RolloutPath { get; }

    private readonly bool _createdCodexHome;
    private readonly bool _createdLogDir;
    private readonly bool _rolloutExistedAtEntry;

    public CodexSessionScope(string workspace)
    {
        Workspace = workspace;
        CodexHome = Path.Combine(Workspace, ".codex");

        var hadCodexHome = Directory.Exists(CodexHome);
        Directory.CreateDirectory(CodexHome);
        _createdCodexHome = !hadCodexHome;

        LogDir = Path.Combine(CodexHome, "log");
        var hadLogDir = Directory.Exists(LogDir);
        Directory.CreateDirectory(LogDir);
        _createdLogDir = !hadLogDir;

        var defaultPath = Path.Combine(LogDir, "codex.rollout.jsonl");
        if (File.Exists(defaultPath))
        {
            // Avoid clobbering an existing log: use a unique per-run file name.
            var uniqueName = $"codex.rollout.{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.{Environment.ProcessId}.{Guid.NewGuid():N}.jsonl";
            RolloutPath = Path.Combine(LogDir, uniqueName);
        }
        else
        {
            RolloutPath = defaultPath;
        }

        // Track whether the chosen rollout path existed on entry (it generally should not).
        _rolloutExistedAtEntry = File.Exists(RolloutPath);
    }

    public ValueTask DisposeAsync()
    {
        // Only clean up aggressively if we created the session directories.
        if (_createdCodexHome)
        {
            DeleteRolloutIfNew();
            TryDeleteIfEmpty(LogDir);
            TryDeleteIfEmpty(CodexHome);
        }
        else if (_createdLogDir)
        {
            // If only the log dir was created, best-effort cleanup inside it.
            DeleteRolloutIfNew();
            TryDeleteIfEmpty(LogDir);
        }
        else if (!_rolloutExistedAtEntry)
        {
            // The directories pre-existed, but the rollout file was created during this session.
            // Remove it to avoid leaving generated state behind.
            try { if (File.Exists(RolloutPath)) File.Delete(RolloutPath); } catch { }
        }

        return ValueTask.CompletedTask;
    }

    private void DeleteRolloutIfNew()
    {
        try
        {
            if (!_rolloutExistedAtEntry && File.Exists(RolloutPath)) File.Delete(RolloutPath);
        }
        catch { }
    }

    private static void TryDeleteIfEmpty(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            using var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            if (e.MoveNext()) return; // not empty
            Directory.Delete(path);
        }
        catch { }
    }
}
