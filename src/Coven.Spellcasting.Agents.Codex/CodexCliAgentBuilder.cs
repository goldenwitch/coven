// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Microsoft.Extensions.Logging;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Rollout;

namespace Coven.Spellcasting.Agents.Codex;

    public static class CodexCliAgentBuilder
    {
        // Typed builder that requires a translator at compile time
        public static ICovenAgent<TMessage> Create<TMessage>(
            string executablePath,
            string workspaceDirectory,
            IScrivener<TMessage> scrivener,
            ICodexRolloutTranslator<TMessage> translator,
            string? shimExecutablePath = null,
            IReadOnlyList<string>? configOverrides = null,
            IMcpServerHost? host = null,
            ITailMuxFactory? tailFactory = null,
            ICodexConfigWriter? configWriter = null,
            ILogger<CodexCliAgent<TMessage>>? logger = null)
            where TMessage : notnull
        {
            return new CodexCliAgent<TMessage>(
                executablePath,
                workspaceDirectory,
                scrivener,
                translator,
                shimExecutablePath,
                configOverrides,
                host,
                tailFactory,
                configWriter,
                logger);
        }
    }
