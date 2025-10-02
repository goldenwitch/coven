namespace Coven.Chat.Discord;

/// <summary>
/// Represents a scoped Discord connection for a single configured channel.
/// The session provides an async stream for inbound messages and a method
/// to send outbound messages. Dispose the session to disconnect.
/// </summary>
internal interface IDiscordSession : IAsyncDisposable
{
    /// <summary>
    /// Streams inbound messages from the configured channel as a live, open-ended async sequence.
    /// The enumeration does not stop when the current backlog is consumed; it awaits new messages and
    /// continues yielding until either the provided <paramref name="cancellationToken"/> is canceled or the
    /// session is disposed (which completes the underlying channel).
    ///
    /// Notes:
    /// - The stream is hot and shared by the underlying channel; multiple concurrent enumerations will compete
    ///   for messages (each message is delivered to a single reader).
    /// - To end the stream, cancel the token you passed to this method or dispose the session.
    /// </summary>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the enumeration.</param>
    /// <returns>An async sequence of inbound Discord messages.</returns>
    IAsyncEnumerable<DiscordIncoming> ReadIncomingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends a message to the configured channel.
    /// </summary>
    /// <param name="text">The raw message content to send.</param>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the operation.</param>
    Task SendAsync(string text, CancellationToken cancellationToken = default);
}
