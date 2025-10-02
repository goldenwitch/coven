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

        // Create a session-scoped CTS that links to the factory-provided token so either can trigger graceful shutdown.
        CancellationTokenSource sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        DiscordLog.SessionOpenStart(_logger, _configuration.ChannelId);
        DiscordGatewayConnection gateway = new(
            _configuration,
            socketClient,
            _logger,
            inboundChannel.Writer,
            sessionCts.Token);
        try
        {
            await gateway.ConnectAsync(sessionCts.Token).ConfigureAwait(false);
            DiscordLog.SessionOpenSucceeded(_logger, _configuration.ChannelId);
            return new DiscordNetSession(gateway, inboundChannel.Reader, sessionCts, _logger);
        }
        catch (OperationCanceledException)
        {
            // Ensure cleanup and wake any reader enumerations that may have started during a race.
            inboundChannel.Writer.TryComplete();
            try { sessionCts.Cancel(); } catch { /* best effort */ }
            try { await gateway.DisposeAsync().ConfigureAwait(false); } catch (Exception dex) { DiscordLog.SessionOpenCleanupDisposeError(_logger, _configuration.ChannelId, dex); }
            sessionCts.Dispose();
            DiscordLog.SessionOpenCanceled(_logger, _configuration.ChannelId);
            throw;
        }
        catch (Exception ex)
        {
            // Ensure cleanup and wake any reader enumerations that may have started during a race.
            inboundChannel.Writer.TryComplete();
            try { sessionCts.Cancel(); } catch { /* best effort */ }
            try { await gateway.DisposeAsync().ConfigureAwait(false); } catch (Exception dex) { DiscordLog.SessionOpenCleanupDisposeError(_logger, _configuration.ChannelId, dex); }
            sessionCts.Dispose();
            DiscordLog.SessionOpenFailed(_logger, _configuration.ChannelId, ex);
            throw;
        }
    }
}
