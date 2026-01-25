// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Channels;
using Coven.Chat.Discord;

namespace Coven.Testing.Harness;

/// <summary>
/// Represents an outbound Discord message sent through the virtual gateway.
/// </summary>
/// <param name="ChannelId">The target Discord channel ID.</param>
/// <param name="Content">The message content.</param>
/// <param name="SentAt">When the message was sent.</param>
public sealed record OutboundMessage(ulong ChannelId, string Content, DateTimeOffset SentAt);

/// <summary>
/// Virtual Discord gateway implementation for E2E testing.
/// Allows tests to simulate inbound messages and inspect outbound messages.
/// </summary>
public sealed class VirtualDiscordGateway : IDiscordGateway
{
    private readonly Channel<DiscordInboundMessage> _inbound = Channel.CreateUnbounded<DiscordInboundMessage>();
    private readonly List<OutboundMessage> _sent = [];
    private readonly Lock _sentLock = new();
    private readonly TaskCompletionSource _readyTcs = new();

    // === Test Setup API ===

    /// <summary>
    /// Simulates a user message arriving on a Discord channel.
    /// </summary>
    /// <param name="channelId">The channel where the message was posted.</param>
    /// <param name="author">The username of the message author.</param>
    /// <param name="content">The message content.</param>
    /// <param name="messageId">Optional message ID (generated if not provided).</param>
    /// <param name="timestamp">Optional timestamp (uses current time if not provided).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask SimulateUserMessageAsync(
        ulong channelId,
        string author,
        string content,
        string? messageId = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        await SimulateMessageAsync(
            channelId,
            author,
            content,
            messageId,
            timestamp,
            isBot: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Simulates any message arriving on a Discord channel (including bot messages).
    /// </summary>
    /// <param name="channelId">The channel where the message was posted.</param>
    /// <param name="author">The username of the message author.</param>
    /// <param name="content">The message content.</param>
    /// <param name="messageId">Optional message ID (generated if not provided).</param>
    /// <param name="timestamp">Optional timestamp (uses current time if not provided).</param>
    /// <param name="isBot">Whether the author is a bot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask SimulateMessageAsync(
        ulong channelId,
        string author,
        string content,
        string? messageId = null,
        DateTimeOffset? timestamp = null,
        bool isBot = false,
        CancellationToken cancellationToken = default)
    {
        await _inbound.Writer.WriteAsync(new DiscordInboundMessage(
            channelId,
            author,
            content,
            messageId ?? Guid.NewGuid().ToString("N"),
            timestamp ?? DateTimeOffset.UtcNow,
            isBot), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals that no more inbound messages will arrive.
    /// The inbound message stream will complete after all buffered messages are consumed.
    /// </summary>
    public void CompleteInbound()
    {
        _inbound.Writer.Complete();
    }

    // === Test Output API ===

    /// <summary>
    /// Gets all messages that have been sent through this gateway.
    /// </summary>
    public IReadOnlyList<OutboundMessage> SentMessages
    {
        get
        {
            lock (_sentLock)
            {
                return [.. _sent];
            }
        }
    }

    /// <summary>
    /// Clears the sent messages list.
    /// </summary>
    public void ClearSentMessages()
    {
        lock (_sentLock)
        {
            _sent.Clear();
        }
    }

    // === IDiscordGateway Implementation ===

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _readyTcs.TrySetResult();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (DiscordInboundMessage message in _inbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            // Filter out bot messages to match DiscordNetGateway behavior and prevent feedback loops.
            // This is the standard behavior - bots should not respond to other bots.
            if (message.IsBot)
            {
                continue;
            }

            yield return message;
        }
    }

    /// <inheritdoc />
    public Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken)
    {
        lock (_sentLock)
        {
            _sent.Add(new OutboundMessage(channelId, content, DateTimeOffset.UtcNow));
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
