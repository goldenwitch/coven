using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord.Di;

/// <summary>
/// Internal DI wiring helpers for the Discord session components.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Discord session factory and its configuration using simple parameters.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="botToken">The bot token used to authenticate with Discord.</param>
    /// <param name="channelId">The channel identifier to bind for inbound and outbound messages.</param>
    /// <returns>The same service collection for chaining.</returns>
    internal static IServiceCollection AddDiscordSessionFactory(this IServiceCollection services, string botToken, ulong channelId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(botToken);

        services.TryAddSingleton(new DiscordClientConfig { BotToken = botToken, ChannelId = channelId });
        services.TryAddSingleton<IDiscordSessionFactory>(sp =>
        {
            DiscordClientConfig config = sp.GetRequiredService<DiscordClientConfig>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger("Coven.Chat.Discord");
            return new DiscordNetSessionFactory(config, logger);
        });

        return services;
    }
}

