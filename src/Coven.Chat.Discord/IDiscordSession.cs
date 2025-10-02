namespace Coven.Chat.Discord;

/// <summary>
/// Represents a scoped Discord connection for a single configured channel.
/// The session provides an async stream for inbound messages and a method
/// to send outbound messages. Dispose the session to disconnect.
/// </summary>
internal interface IDiscordSession : IAsyncDisposable
{
    /// <summary>
    /// Streams inbound messages from the configured channel as a cooperative cancellation-aware async sequence.
    /// The sequence completes when the provided <paramref name="cancellationToken"/> is canceled or when the
    /// session is disposed.
    /// </summary>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the enumeration.</param>
    /// <returns>An async sequence of inbound Discord messages.</returns>
    IAsyncEnumerable<DiscordIncoming> ReadIncomingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the configured channel.
    /// </summary>
    /// <param name="text">The raw message content to send.</param>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the operation.</param>
    Task SendAsync(string text, CancellationToken cancellationToken = default);
}
