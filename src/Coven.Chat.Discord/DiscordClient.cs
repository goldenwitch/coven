namespace Coven.Chat.Discord;

// Minimal client surface for treating a single Discord channel/thread as a journal.
internal interface IDiscordClient
{
    // Stream new messages after an optional message id anchor.
    IAsyncEnumerable<DiscordMessage> WatchAsync(string channelId, string? afterMessageId = null, CancellationToken cancellationToken = default);

    // Fetch historical messages after an optional message id anchor.
    Task<IReadOnlyList<DiscordMessage>> GetHistoryAsync(string channelId, string? afterMessageId = null, CancellationToken cancellationToken = default);

    // Send a message to the channel.
    Task SendAsync(string channelId, string text, CancellationToken cancellationToken = default);
}

internal sealed record DiscordMessage(string Id, string Author, string Text, DateTimeOffset Timestamp);
