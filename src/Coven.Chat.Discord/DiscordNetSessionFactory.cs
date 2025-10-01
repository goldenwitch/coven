using Discord;
using Discord.WebSocket;

namespace Coven.Chat.Discord;

/// <summary>
/// Discord.Net-backed implementation of <see cref="IDiscordSessionFactory"/> that
/// creates sessions owning their own gateway connection and lifecycle.
/// </summary>
public sealed class DiscordNetSessionFactory(DiscordClientConfig configuration) : IDiscordSessionFactory
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <inheritdoc />
    public async Task<IDiscordSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        DiscordSocketConfig socketConfiguration = new()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = false
        };

        DiscordSocketClient socketClient = new(socketConfiguration);
        DiscordNetSession session = new(_configuration, socketClient);

        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}

