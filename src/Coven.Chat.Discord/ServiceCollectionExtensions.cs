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

public static class ServiceCollectionExtensions
{
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

        // Enable chat shattering + windowing by default
        services.AddChatShattering();
        services.AddChatWindowing();

        // Discord shattering:
        // - Incoming: paragraph boundaries -> ChatChunk
        // - Outgoing: enforce 2k max per message -> ChatOutgoing shards
        services.AddScoped<IShatterPolicy<ChatEntry>>(sp =>
            new ChainedShatterPolicy<ChatEntry>(
                new ChatParagraphShatterPolicy(),
                new ChatOutgoingMaxLengthShatterPolicy(2000)
            ));

        services.TryAddScoped<IWindowPolicy<ChatChunk>>(_ =>
            new CompositeWindowPolicy<ChatChunk>(
                new ChatParagraphWindowPolicy()
            ));


        return services;
    }
}
