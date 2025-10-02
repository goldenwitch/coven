namespace Coven.Chat.Discord;

/// <summary>
/// Factory abstraction for creating a scoped Discord session. Each session
/// owns its connection and disposes it when the scope ends.
/// </summary>
internal interface IDiscordSessionFactory
{
    /// <summary>
    /// Opens a new Discord session that connects to the gateway and binds to the configured channel.
    /// The returned session must be disposed to cleanly disconnect.
    /// </summary>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the operation.</param>
    /// <returns>An active Discord session.</returns>
    Task<IDiscordSession> OpenAsync(CancellationToken cancellationToken = default);
}
