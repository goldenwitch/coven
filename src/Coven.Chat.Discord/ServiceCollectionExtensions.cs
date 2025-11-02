using Coven.Core;
using Coven.Core.Streaming;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Coven.Daemonology;
using Coven.Transmutation;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Coven.Chat.Windowing;
using Coven.Chat.Shattering;

namespace Coven.Chat.Discord;

/// <summary>
/// Dependency Injection helpers for wiring the Discord chat adapter.
/// Registers the Discord client, session factory, journals, transmuter, daemon, and default windowing policies.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Discord chat integration using the provided client configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="discordClientConfig">Configuration including bot token and channel id.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddDiscordChat(this IServiceCollection services, DiscordClientConfig discordClientConfig)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Discord client and session factory
        services.AddScoped(sp => discordClientConfig);
        // Configure DiscordSocketClient with required gateway intents so MessageReceived fires.
        // Intents must also be enabled in the Discord Developer Portal (Message Content is privileged).
        services.AddScoped(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildMessages |
                GatewayIntents.DirectMessages |
                GatewayIntents.MessageContent,
        }));
        services.AddScoped<DiscordChatSessionFactory>();
        services.AddScoped<DiscordGatewayConnection>();

        // Default ChatEntry journal if none provided by host
        services.TryAddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
        services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>("Coven.InternalDiscordScrivener");

        services.AddScoped<IBiDirectionalTransmuter<DiscordEntry, ChatEntry>, DiscordTransmuter>();
        services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
        services.AddScoped<ContractDaemon, DiscordChatDaemon>();

        // Enable windowing; session performs shattering for drafts
        services.AddChatWindowing();

        // Session-local shattering policy (drafts -> chunks): Paragraph first, then 2k safety split
        services.TryAddScoped<IShatterPolicy<ChatEntry>>(sp =>
            new ChainedShatterPolicy<ChatEntry>(
                new ChatParagraphShatterPolicy(),
                new ChatChunkMaxLengthShatterPolicy(2000)
            ));

        // Compose paragraph boundary with Discord-safe 2k cap
        services.TryAddScoped<IWindowPolicy<ChatChunk>>(_ =>
            new CompositeWindowPolicy<ChatChunk>(
                new ChatParagraphWindowPolicy(),
                new ChatMaxLengthWindowPolicy(2000)
            ));


        return services;
    }
}
