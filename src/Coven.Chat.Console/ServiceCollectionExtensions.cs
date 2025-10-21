using Coven.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Chat.Console;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsoleChat(this IServiceCollection services, ConsoleClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(sp => config);
        services.AddScoped<ConsoleGatewayConnection>();
        services.AddScoped<ConsoleChatSessionFactory>();

        // Default ChatEntry journal if none provided by host
        services.TryAddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddScoped<IScrivener<ConsoleEntry>, ConsoleScrivener>();
        services.AddKeyedScoped<IScrivener<ConsoleEntry>, InMemoryScrivener<ConsoleEntry>>("Coven.InternalConsoleScrivener");

        services.AddScoped<IBiDirectionalTransmuter<ConsoleEntry, ChatEntry>, ConsoleTransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<ContractDaemon, ConsoleChatDaemon>();
        return services;
    }
}
