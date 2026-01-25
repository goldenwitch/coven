// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Discord;

/// <summary>
/// Abstracts Discord connectivity for inbound message reception and outbound message dispatch.
/// Implementations may connect to real Discord (via Discord.Net) or provide virtualized
/// gateways for testing.
/// </summary>
public interface IDiscordGateway : IAsyncDisposable
{
    /// <summary>
    /// Establishes the Discord connection and begins receiving messages.
    /// For real gateways, this performs login and starts the WebSocket connection.
    /// Returns when the gateway is ready to send/receive messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the connection attempt.</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronous stream of inbound Discord messages. The stream yields messages
    /// as they arrive and completes when the gateway disconnects or is disposed.
    /// </summary>
    /// <remarks>
    /// Implementations should filter bot-authored messages before yielding, unless
    /// <see cref="DiscordGatewayOptions.IncludeBotMessages"/> is explicitly set.
    /// </remarks>
    IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends a message to the specified Discord channel.
    /// </summary>
    /// <param name="channelId">The target channel ID.</param>
    /// <param name="content">The message content to send.</param>
    /// <param name="cancellationToken">Cancellation token for the send operation.</param>
    Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken);
}
