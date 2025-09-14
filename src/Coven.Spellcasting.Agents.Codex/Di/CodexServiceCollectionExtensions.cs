// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents.Validation;
using Coven.Core;
// duplicate using removed

namespace Coven.Spellcasting.Agents.Codex.Di;

public sealed class CodexCliAgentRegistrationOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkspaceDirectory { get; set; } = Directory.GetCurrentDirectory();
    public string? ShimExecutablePath { get; set; }
}

    public static class CodexServiceCollectionExtensions
    {
        // Default registration: ChatEntry + DefaultChatEntryTranslator
        public static IServiceCollection AddCodexCliAgent(
            this IServiceCollection services,
            Action<CodexCliAgentRegistrationOptions> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var opts = new CodexCliAgentRegistrationOptions();
            configure(opts);
            if (string.IsNullOrWhiteSpace(opts.ExecutablePath)) throw new ArgumentException("ExecutablePath must be provided.");
            if (string.IsNullOrWhiteSpace(opts.WorkspaceDirectory)) throw new ArgumentException("WorkspaceDirectory must be provided.");

            services.AddCovenAgent(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<ChatEntry>>();
                var translator = sp.GetService<ICodexRolloutTranslator<ChatEntry>>()
                                 ?? new DefaultChatEntryTranslator();
                var host = sp.GetService<IMcpServerHost>() ?? new LocalMcpServerHost(opts.WorkspaceDirectory);
                var procFactory = sp.GetService<ICodexProcessFactory>() ?? new DefaultCodexProcessFactory();
                var tailFactory = sp.GetService<ITailMuxFactory>() ?? new DefaultTailMuxFactory();
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var resolver = sp.GetService<IRolloutPathResolver>() ?? new DefaultRolloutPathResolver();
                // Spells are registered at runtime via MagikUser → agent.RegisterSpells

                // Auto-discover shim path if not provided: look under AppContext.BaseDirectory/mcp-shim
                var shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);

                return CodexCliAgentBuilder.Create(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    scrivener,
                    translator,
                    shimPath,
                    host,
                    procFactory,
                    tailFactory,
                    configWriter,
                    resolver);
            });

            // Register validator using the same options and auto-discovery resolver
            services.AddSingleton<IAgentValidation>(sp =>
            {
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var spellbook = sp.GetService<Spellbook>();
                string? shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);
                return new CodexCliValidation(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    shimPath,
                    spellbook,
                    configWriter);
            });

            return services;
        }

        // Typed registration that enforces a translator at compile time
        public static IServiceCollection AddCodexCliAgent<TMessage, TTranslator>(
            this IServiceCollection services,
            Action<CodexCliAgentRegistrationOptions> configure)
            where TMessage : notnull
            where TTranslator : class, ICodexRolloutTranslator<TMessage>, new()
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var opts = new CodexCliAgentRegistrationOptions();
            configure(opts);
            if (string.IsNullOrWhiteSpace(opts.ExecutablePath)) throw new ArgumentException("ExecutablePath must be provided.");
            if (string.IsNullOrWhiteSpace(opts.WorkspaceDirectory)) throw new ArgumentException("WorkspaceDirectory must be provided.");

            services.AddCovenAgent(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<TMessage>>();
                var translator = sp.GetService<ICodexRolloutTranslator<TMessage>>() ?? new TTranslator();
                var host = sp.GetService<IMcpServerHost>() ?? new LocalMcpServerHost(opts.WorkspaceDirectory);
                var procFactory = sp.GetService<ICodexProcessFactory>() ?? new DefaultCodexProcessFactory();
                var tailFactory = sp.GetService<ITailMuxFactory>() ?? new DefaultTailMuxFactory();
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var resolver = sp.GetService<IRolloutPathResolver>() ?? new DefaultRolloutPathResolver();

                // Auto-discover shim path if not provided
                var shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);

                return CodexCliAgentBuilder.Create(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    scrivener,
                    translator,
                    shimPath,
                    host,
                    procFactory,
                    tailFactory,
                    configWriter,
                    resolver);
            });

            // Register validator using the same options and auto-discovery resolver
            services.AddSingleton<IAgentValidation>(sp =>
            {
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var spellbook = sp.GetService<Spellbook>();
                string? shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);
                return new CodexCliValidation(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    shimPath,
                    spellbook,
                    configWriter);
            });

            return services;
        }

        private static string? AutoDiscoverShimIfMissing(string? provided)
        {
            if (!string.IsNullOrWhiteSpace(provided)) return provided;
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var shimDir = Path.Combine(baseDir, "mcp-shim");
                if (Directory.Exists(shimDir))
                {
                    var exe = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.exe");
                    var dll = Path.Combine(shimDir, "Coven.Spellcasting.Agents.Codex.McpShim.dll");
                    if (File.Exists(exe)) return exe;
                    if (File.Exists(dll)) return dll;
                    var any = Directory.GetFiles(shimDir).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(any)) return any;
                }
            }
            catch { }
            return null;
        }
    }
