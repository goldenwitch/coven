// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Discord;

/// <summary>
/// Represents a message received from Discord, normalized for Coven processing.
/// </summary>
/// <param name="ChannelId">The Discord channel where the message was posted.</param>
/// <param name="Author">The username of the message author.</param>
/// <param name="Content">The message text content.</param>
/// <param name="MessageId">Discord's unique message identifier (snowflake as string).</param>
/// <param name="Timestamp">When the message was created on Discord.</param>
/// <param name="IsBot">Whether the author is a bot account.</param>
public sealed record DiscordInboundMessage(
    ulong ChannelId,
    string Author,
    string Content,
    string MessageId,
    DateTimeOffset Timestamp,
    bool IsBot);
