using Coven.Core;
using Coven.Daemonology;
using Coven.Transmutation;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Chat.Discord;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordChat(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Discord client and gateway factory
        services.AddSingleton<DiscordSocketClient>();
        services.AddScoped<DiscordGatewayFactory>();

        services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>("Coven.InternalDiscordScrivener");
        services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
        services.AddScoped<IBiDirectionalTransmuter<DiscordEntry, ChatEntry>, DiscordTransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<IDaemon, DiscordChatDaemon>();
        return services;
    }
}
