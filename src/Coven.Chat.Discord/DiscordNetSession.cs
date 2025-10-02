using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Discord.Net-backed session that owns a Discord gateway connection and exposes
/// an async stream for inbound messages alongside a simple send method.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiscordNetSession"/> class.
/// </remarks>
/// <param name="configuration">The minimal configuration used to bind a channel.</param>
/// <param name="socketClient">The Discord.Net socket client used by this session.</param>
internal sealed class DiscordNetSession(DiscordClientConfig configuration, DiscordSocketClient socketClient, ILogger logger) : IDiscordSession
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly DiscordSocketClient _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
    private readonly Channel<DiscordIncoming> _inboundChannel = Channel.CreateBounded<DiscordIncoming>(new BoundedChannelOptions(256)
    {
        SingleReader = false,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    private bool _disposed;


    /// <summary>
    /// Connects the underlying socket client and subscribes message events.
    /// Intended to be called by the session factory.
    /// </summary>
    /// <param name="cancellationToken">Token that requests cooperative cancellation of the operation.</param>
    internal async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DiscordNetSession));
        await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DiscordLog.Connecting(_logger, _configuration.ChannelId);
            _socketClient.MessageReceived += OnMessageReceivedAsync;
            await _socketClient.LoginAsync(TokenType.Bot, _configuration.BotToken).ConfigureAwait(false);
            await _socketClient.StartAsync().ConfigureAwait(false);
            DiscordLog.Connected(_logger);
        }
        finally
        {
            _lifecycleSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DiscordIncoming> ReadIncomingAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DiscordNetSession));
        ChannelReader<DiscordIncoming> reader = _inboundChannel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out DiscordIncoming? next))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return next;
            }
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(DiscordNetSession));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        IMessageChannel channel = _socketClient.GetChannel(_configuration.ChannelId) as IMessageChannel
            ?? throw new InvalidOperationException("Configured channel was not found or is not a message channel.");

        await channel.SendMessageAsync(text).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message.Channel is null || message.Channel.Id != _configuration.ChannelId)
        {
            return;
        }

        DiscordIncoming incoming = new(
            Sender: message.Author?.Username ?? message.Author?.Id.ToString(CultureInfo.InvariantCulture) ?? "unknown",
            Text: message.Content ?? string.Empty,
            MessageId: message.Id.ToString(CultureInfo.InvariantCulture),
            Timestamp: message.Timestamp);

        await _inboundChannel.Writer.WriteAsync(incoming).ConfigureAwait(false);
        DiscordLog.InboundMessage(_logger, incoming.MessageId, incoming.Sender);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _lifecycleSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _socketClient.MessageReceived -= OnMessageReceivedAsync;
            _inboundChannel.Writer.TryComplete();

            await _socketClient.LogoutAsync().ConfigureAwait(false);
            await _socketClient.StopAsync().ConfigureAwait(false);
            _socketClient.Dispose();
            DiscordLog.Disconnected(_logger);
        }
        finally
        {
            _lifecycleSemaphore.Release();
        }

        _lifecycleSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
