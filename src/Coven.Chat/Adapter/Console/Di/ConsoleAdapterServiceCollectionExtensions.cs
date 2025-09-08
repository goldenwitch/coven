namespace Coven.Chat.Adapter.Console.Di;

using Microsoft.Extensions.DependencyInjection;

public static class ConsoleAdapterServiceCollectionExtensions
{
    /// <summary>
    /// Register a working Console chat adapter stack:
    /// - IConsoleIO (DefaultConsoleIO)
    /// - ConsoleAdapterOptions (optional configure)
    /// - IAdapter&lt;ChatEntry&gt; (ConsoleAdapter)
    /// - IAdapterHost&lt;ChatEntry&gt; (SimpleAdapterHost)
    /// - IScrivener&lt;ChatEntry&gt; (InMemoryScrivener) if none already registered
    /// </summary>
    public static IServiceCollection AddConsoleChatAdapter(
        this IServiceCollection services,
        Action<ConsoleAdapterOptions>? configure = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Options
        var opts = new ConsoleAdapterOptions();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        // IO abstraction
        bool hasIo = false;
        foreach (var sd in services)
        {
            if (sd.ServiceType == typeof(IConsoleIO)) { hasIo = true; break; }
        }
        if (!hasIo) services.AddSingleton<IConsoleIO, DefaultConsoleIO>();

        // Scrivener default if none registered
        bool hasScrivener = false;
        foreach (var sd in services)
        {
            if (sd.ServiceType == typeof(IScrivener<ChatEntry>)) { hasScrivener = true; break; }
        }
        if (!hasScrivener) services.AddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        // Adapter + Host
        services.AddSingleton<IAdapter<ChatEntry>, ConsoleAdapter>();
        services.AddSingleton<IAdapterHost<ChatEntry>, SimpleAdapterHost<ChatEntry>>();

        return services;
    }
}

