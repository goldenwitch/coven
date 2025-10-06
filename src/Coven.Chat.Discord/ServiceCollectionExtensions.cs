using Coven.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Discord client and session factory
        services.AddSingleton<DiscordSocketClient>();
        services.AddScoped<DiscordChatSessionFactory>();

        // Default ChatEntry journal if none provided by host
        services.TryAddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>("Coven.InternalDiscordScrivener");
        services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
        services.AddScoped<IBiDirectionalTransmuter<DiscordEntry, ChatEntry>, DiscordTransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<IDaemon, DiscordChatDaemon>();
        return services;
    }
}
