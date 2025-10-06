namespace Coven.Chat.Discord;

/// <summary>
/// Minimal configuration required by the Discord client adapter.
/// </summary>
public sealed class DiscordClientConfig
{
    /// <summary>
    /// Gets or sets the bot token used to authenticate with the Discord gateway.
    /// </summary>
    public required string BotToken { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the channel to read from and write to.
    /// </summary>
    public ulong ChannelId { get; init; }
}
