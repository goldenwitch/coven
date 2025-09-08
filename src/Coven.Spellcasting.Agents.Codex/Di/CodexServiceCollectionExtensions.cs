using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;

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
    public static IServiceCollection AddCodexCliAgent<TMessageFormat>(
        this IServiceCollection services,
        Action<CodexCliAgentRegistrationOptions> configure)
        where TMessageFormat : notnull
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var opts = new CodexCliAgentRegistrationOptions();
        configure(opts);
        if (string.IsNullOrWhiteSpace(opts.ExecutablePath)) throw new ArgumentException("ExecutablePath must be provided.");
        if (string.IsNullOrWhiteSpace(opts.WorkspaceDirectory)) throw new ArgumentException("WorkspaceDirectory must be provided.");

        services.AddSingleton<ICovenAgent<TMessageFormat>>(sp =>
        {
            var scrivener = sp.GetRequiredService<IScrivener<TMessageFormat>>();

            var host = sp.GetService<MCP.IMcpServerHost>() ?? new MCP.LocalMcpServerHost(opts.WorkspaceDirectory);
            var procFactory = sp.GetService<Processes.ICodexProcessFactory>() ?? new Processes.DefaultCodexProcessFactory();
            var tailFactory = sp.GetService<Tail.ITailMuxFactory>() ?? new Tail.DefaultTailMuxFactory();
            var configWriter = sp.GetService<Config.ICodexConfigWriter>() ?? new Config.DefaultCodexConfigWriter();
            var resolver = sp.GetService<Rollout.IRolloutPathResolver>() ?? new Rollout.DefaultRolloutPathResolver();

            return new CodexCliAgent<TMessageFormat>(
                opts.ExecutablePath,
                opts.WorkspaceDirectory,
                scrivener,
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

