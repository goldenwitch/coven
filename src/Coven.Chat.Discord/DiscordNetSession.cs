using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Coven.Chat.Discord;

/// <summary>
/// Discord.Net-backed session that owns a <see cref="DiscordGatewayConnection"/>
/// and exposes an async stream for inbound messages alongside a simple send method.
/// Connection is established by the factory before the session is constructed.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiscordNetSession"/> class.
/// </remarks>
/// <param name="gateway">The connected gateway connection owned by this session.</param>
/// <param name="inboundReader">The channel reader for inbound messages.</param>
internal sealed class DiscordNetSession(DiscordGatewayConnection gateway, ChannelReader<DiscordIncoming> inboundReader) : IDiscordSession
{
    private readonly DiscordGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly ChannelReader<DiscordIncoming> _inboundReader = inboundReader ?? throw new ArgumentNullException(nameof(inboundReader));
    // Flag domain: 0 = not disposed, 1 = disposed. Used with Interlocked/Volatile.
    private int _disposed;

    /// <inheritdoc />
    public async IAsyncEnumerable<DiscordIncoming> ReadIncomingAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Volatile.Read provides a cheap, up-to-date disposed check across threads without locking.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(DiscordNetSession));

        await foreach (DiscordIncoming nextIncoming in _inboundReader.ReadAllAsync(cancellationToken))
        {
            yield return nextIncoming;
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        // Volatile.Read provides a cheap, up-to-date disposed check across threads without locking.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(DiscordNetSession));

        await _gateway.SendAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Interlocked.Exchange gates disposal so only one caller runs the critical section.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
        await _gateway.DisposeAsync().ConfigureAwait(false);
    }
}
