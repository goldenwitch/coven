using System.Threading.Channels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Discord.Net-backed implementation of <see cref="IDiscordSessionFactory"/> that
/// creates sessions owning their own gateway connection and lifecycle.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiscordNetSessionFactory"/> class.
/// </remarks>
/// <param name="configuration">The minimal configuration used by sessions.</param>
internal sealed class DiscordNetSessionFactory(DiscordClientConfig configuration, ILogger logger) : IDiscordSessionFactory
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<IDiscordSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        DiscordSocketConfig socketConfiguration = new()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = false
        };

        DiscordSocketClient socketClient = new(socketConfiguration);

        // The bounded channel coordinates inbound message flow from the gateway to session consumers.
        BoundedChannelOptions channelOptions = new(256)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };
        Channel<DiscordIncoming> inboundChannel = Channel.CreateBounded<DiscordIncoming>(channelOptions);

        DiscordGatewayConnection gateway = new(_configuration, socketClient, _logger, inboundChannel.Writer);
        try
        {
            await gateway.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return new DiscordNetSession(gateway, inboundChannel.Reader);
        }
        catch
        {
            // Ensure cleanup and wake any reader enumerations that may have started during a race.
            inboundChannel.Writer.TryComplete();
            await gateway.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
