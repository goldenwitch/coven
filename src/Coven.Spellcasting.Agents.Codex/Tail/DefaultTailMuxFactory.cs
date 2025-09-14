// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tail;

internal sealed class DefaultTailMuxFactory : ITailMuxFactory
{
    public ITailMux CreateForRollout(string rolloutPath, string codexExecutablePath, string workspaceDirectory)
    {
        var codexHome = Path.Combine(workspaceDirectory, ".codex");
        try { Directory.CreateDirectory(codexHome); } catch { }

        var env = new Dictionary<string, string?>
        {
            ["CODEX_HOME"] = codexHome
        };

        // Start codex with an explicit log directory under the workspace
        var args = new[] { "--log-dir", codexHome } as IReadOnlyList<string>;
        return new ProcessDocumentTailMux(
            rolloutPath,
            fileName: codexExecutablePath,
            arguments: args,
            workingDirectory: workspaceDirectory,
            environment: env);
    }
}
