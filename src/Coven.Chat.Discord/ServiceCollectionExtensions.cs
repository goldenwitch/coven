using Coven.Core;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Chat.Discord;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordChat<TEntry>(this IServiceCollection services)
        where TEntry : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>("Coven.InternalDiscordScrivener");
        services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
        services.AddScoped<IBiDirectionalTransmuter<DiscordEntry, ChatEntry>, DiscordTransmuter>();
        services.AddScoped<IDaemon, DiscordChatDaemon>();
        return services;
    }
}
