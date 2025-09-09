using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.Tail;
using Coven.Spellcasting;

namespace Coven.Spellcasting.Agents.Codex.Di;

public sealed class CodexCliAgentRegistrationOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkspaceDirectory { get; set; } = Directory.GetCurrentDirectory();
    public string? ShimExecutablePath { get; set; }
    public IEnumerable<object>? Spells { get; set; }
}

    public static class CodexServiceCollectionExtensions
    {
        // String-only registration
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

            services.AddSingleton<ICovenAgent<string>>(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<string>>();

                var host = sp.GetService<IMcpServerHost>() ?? new LocalMcpServerHost(opts.WorkspaceDirectory);
                var procFactory = sp.GetService<ICodexProcessFactory>() ?? new DefaultCodexProcessFactory();
                var tailFactory = sp.GetService<ITailMuxFactory>() ?? new DefaultTailMuxFactory();
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var resolver = sp.GetService<IRolloutPathResolver>() ?? new DefaultRolloutPathResolver();
                var spells = opts.Spells ?? (sp.GetService<Spellbook>()?.Spells);
                return CodexCliAgentBuilder.CreateString(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    scrivener,
                    opts.ShimExecutablePath,
                    spells,
                    host,
                    procFactory,
                    tailFactory,
                    configWriter,
                    resolver);
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

            services.AddSingleton<ICovenAgent<TMessage>>(sp =>
            {
                var scrivener = sp.GetRequiredService<IScrivener<TMessage>>();
                var translator = sp.GetService<ICodexRolloutTranslator<TMessage>>() ?? new TTranslator();

                var host = sp.GetService<IMcpServerHost>() ?? new LocalMcpServerHost(opts.WorkspaceDirectory);
                var procFactory = sp.GetService<ICodexProcessFactory>() ?? new DefaultCodexProcessFactory();
                var tailFactory = sp.GetService<ITailMuxFactory>() ?? new DefaultTailMuxFactory();
                var configWriter = sp.GetService<ICodexConfigWriter>() ?? new DefaultCodexConfigWriter();
                var resolver = sp.GetService<IRolloutPathResolver>() ?? new DefaultRolloutPathResolver();

                return CodexCliAgentBuilder.Create(
                    opts.ExecutablePath,
                    opts.WorkspaceDirectory,
                    scrivener,
                    translator,
                    opts.ShimExecutablePath,
                    opts.Spells,
                    host,
                    procFactory,
                    tailFactory,
                    configWriter,
                    resolver);
            });

            return services;
        }
    }
