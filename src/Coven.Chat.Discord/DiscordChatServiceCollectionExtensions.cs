// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Coven.Core.Daemonology;
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
public static class DiscordChatServiceCollectionExtensions
{
    /// <summary>
    /// Adds Discord chat integration using the provided client configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="discordClientConfig">Configuration including bot token and channel id.</param>
    /// <param name="gatewayOptions">Optional gateway options for filtering and behavior configuration.</param>
    /// <returns>The same service collection to enable fluent chaining.</returns>
    public static IServiceCollection AddDiscordChat(
        this IServiceCollection services,
        DiscordClientConfig discordClientConfig,
        DiscordGatewayOptions? gatewayOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Configuration
        services.AddScoped(sp => discordClientConfig);
        services.AddScoped(sp => gatewayOptions ?? new DiscordGatewayOptions());

        // Discord.Net client (used by production gateway)
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

        // Gateway abstraction - can be replaced for testing
        services.AddScoped<IDiscordGateway, DiscordNetGateway>();

        // Gateway connection (consumes IDiscordGateway)
        services.AddScoped<DiscordGatewayConnection>();
        services.AddScoped<DiscordChatSessionFactory>();

        // Default ChatEntry journal if none provided by host
        services.TryAddScoped<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
        services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>("Coven.InternalDiscordScrivener");

        services.AddScoped<IImbuingTransmuter<DiscordEntry, long, ChatEntry>, DiscordTransmuter>();
        services.AddScoped<IImbuingTransmuter<ChatEntry, long, DiscordEntry>, DiscordTransmuter>();
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
