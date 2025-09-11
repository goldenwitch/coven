// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.Rollout;

public interface IRolloutPathResolver
{
    Task<string?> ResolveAsync(
        string codexExecutablePath,
        string workspaceDirectory,
        string codexHomeDir,
        IReadOnlyDictionary<string, string?> env,
        TimeSpan timeout,
        CancellationToken ct);
}