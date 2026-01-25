// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Discord;

/// <summary>
/// Configuration options for Discord gateway behavior.
/// </summary>
public sealed record DiscordGatewayOptions
{
    /// <summary>
    /// When true, bot-authored messages are included in the inbound stream.
    /// Default is false—bot messages are filtered to prevent response loops.
    /// </summary>
    public bool IncludeBotMessages { get; init; }

    /// <summary>
    /// Optional channel filter. When set, only messages from these channels
    /// are included in the inbound stream. When null, all channels are included.
    /// </summary>
    public IReadOnlySet<ulong>? ChannelFilter { get; init; }
}
