// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Config;
using Microsoft.Extensions.Logging;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting.Agents.Validation;

namespace Coven.Spellcasting.Agents.Codex.Di;

public sealed class CodexCliAgentRegistrationOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkspaceDirectory { get; set; } = Directory.GetCurrentDirectory();
    public string? ShimExecutablePath { get; set; }
}

    public static class CodexServiceCollectionExtensions
    {
        private static ICovenAgent<TMessage> BuildAgent<TMessage, TTranslator>(
            IServiceProvider sp,
            CodexCliAgentRegistrationOptions opts)
            where TMessage : notnull
            where TTranslator : class, ICodexRolloutTranslator<TMessage>, new()
        {
            var scrivener = sp.GetRequiredService<IScrivener<TMessage>>();
            var translator = sp.GetService<ICodexRolloutTranslator<TMessage>>() ?? new TTranslator();
            var host = sp.GetService<IMcpServerHost>() ?? new LocalMcpServerHost(opts.WorkspaceDirectory);
            var tailFactory = sp.GetService<ITailMuxFactory>() ?? new DefaultTailMuxFactory();
            var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
            var logger = sp.GetService<ILogger<CodexCliAgent<TMessage>>>();

            var shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);

            return CodexCliAgentBuilder.Create(
                opts.ExecutablePath,
                opts.WorkspaceDirectory,
                scrivener,
                translator,
                shimPath,
                host,
                tailFactory,
                configWriter,
                logger);
        }

        private static void AddValidation(
            IServiceCollection services,
            CodexCliAgentRegistrationOptions opts)
        {
            services.AddSingleton<IAgentValidation>(sp =>
            {
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var spellbook = sp.GetService<Spellbook>();
                var shimPath = AutoDiscoverShimIfMissing(opts.ShimExecutablePath);
                return new CodexCliValidation(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    shimPath,
                    spellbook,
                    configWriter);
            });
        }
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

            services.AddCovenAgent(sp => BuildAgent<ChatEntry, DefaultChatEntryTranslator>(sp, opts));
            AddValidation(services, opts);

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

            services.AddCovenAgent(sp => BuildAgent<TMessage, TTranslator>(sp, opts));
            AddValidation(services, opts);

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
