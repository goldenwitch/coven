// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex;

public static class CodexCliAgentBuilder
{
    // String-only builder (happy path)
    public static ICovenAgent<string> CreateString(
        string executablePath,
        string workspaceDirectory,
        IScrivener<string> scrivener,
        string? shimExecutablePath = null,
        IEnumerable<object>? spells = null,
        IMcpServerHost? host = null,
        ICodexProcessFactory? processFactory = null,
        ITailMuxFactory? tailFactory = null,
        ICodexConfigWriter? configWriter = null,
        IRolloutPathResolver? rolloutResolver = null)
    {
        return new CodexCliAgent<string>(
            executablePath,
            workspaceDirectory,
            scrivener,
            shimExecutablePath,
            spells,
            host,
            processFactory,
            tailFactory,
            configWriter,
            rolloutResolver);
    }

    // Typed builder that requires a translator at compile time
    public static ICovenAgent<TMessage> Create<TMessage>(
        string executablePath,
        string workspaceDirectory,
        IScrivener<TMessage> scrivener,
        ICodexRolloutTranslator<TMessage> translator,
        string? shimExecutablePath = null,
        IEnumerable<object>? spells = null,
        IMcpServerHost? host = null,
        ICodexProcessFactory? processFactory = null,
        ITailMuxFactory? tailFactory = null,
        ICodexConfigWriter? configWriter = null,
        IRolloutPathResolver? rolloutResolver = null)
        where TMessage : notnull
    {
        return new CodexCliAgent<TMessage>(
            executablePath,
            workspaceDirectory,
            scrivener,
            translator,
            shimExecutablePath,
            spells,
            host,
            processFactory,
            tailFactory,
            configWriter,
            rolloutResolver);
    }
}