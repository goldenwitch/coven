using Coven.Core;
using Coven.Core.Builder;
using Coven.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coven.Chat.Discord;

public sealed class DiscordChatAdapterOptions
{
    public string ChannelId { get; init; } = string.Empty;
}

public static class ServiceCollectionExtensions
{
    // Public builder: composes a ContractDaemon for Discord chat.
    public static IServiceCollection AddDiscordChatAdapter(this IServiceCollection services, Action<DiscordChatAdapterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        DiscordChatAdapterOptions options = new();
        configure(options);
        if (string.IsNullOrWhiteSpace(options.ChannelId))
        {
            throw new ArgumentException("ChannelId must be provided.", nameof(configure));
        }
        services.TryAddSingleton(options);

        // Status journal for ContractDaemon promises.
        services.AddInMemoryScrivener<DaemonEvent>();

        // Adapter components.
        services.TryAddSingleton<DiscordScrivener>();
        services.TryAddSingleton<IScrivener<DiscordEntry>>(sp => sp.GetRequiredService<DiscordScrivener>());
        services.TryAddSingleton<IBiDirectionalTransmuter<DiscordEntry, ChatEntry>, DiscordTransmuter>();

        // Daemon exposure: resolve via IDaemon or the concrete type.
        services.TryAddSingleton<IDaemon, DiscordChatDaemon>();
        services.TryAddSingleton<DiscordChatDaemon>();

        return services;
    }
}
