using System.Globalization;
using System.Threading.Channels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Owns a single Discord gateway connection and encapsulates lifecycle concerns
/// such as connecting, event wiring, message dispatch, and disposal.
/// This class is internal and is composed by a higher-level session wrapper.
/// </summary>
/// <param name="configuration">The minimal client configuration for this connection.</param>
/// <param name="socketClient">The Discord.Net socket client to drive the gateway.</param>
/// <param name="logger">The logger used for lifecycle and message breadcrumbs.</param>
/// <param name="inboundWriter">The writer used to publish inbound messages to a bounded channel.</param>
internal sealed class DiscordGatewayConnection(
    DiscordClientConfig configuration,
    DiscordSocketClient socketClient,
    ILogger logger,
    ChannelWriter<DiscordIncoming> inboundWriter) : IAsyncDisposable
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly DiscordSocketClient _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ChannelWriter<DiscordIncoming> _inboundWriter = inboundWriter ?? throw new ArgumentNullException(nameof(inboundWriter));

    // Serialize lifecycle transitions (connect/dispose) to prevent interleaving and double wiring.
    private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);

    // 0 = not disposed, 1 = disposed. Interlocked used to guarantee idempotent disposal.
    private int _disposed;

    // 0 = not wired, 1 = wired. Interlocked used to guarantee single subscription to events.
    private int _eventsWired;

    /// <summary>
    /// Connects to Discord, authenticates the bot, starts the gateway, and wires message handlers.
    /// Idempotent with respect to event wiring: only the first call will subscribe handlers.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the connection attempt.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Volatile.Read provides a cheap, up-to-date disposed check across threads without locking.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(DiscordGatewayConnection));

        // Ensure only one concurrent ConnectAsync or DisposeAsync runs at a time.

        await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool connected = false;
        try
        {
            DiscordLog.Connecting(_logger, _configuration.ChannelId);

            // Wire events exactly once even if ConnectAsync is somehow invoked multiple times by upstream code.
            if (Interlocked.CompareExchange(ref _eventsWired, 1, 0) == 0)
            {
                _socketClient.MessageReceived += OnMessageReceivedAsync;
            }

            await _socketClient.LoginAsync(TokenType.Bot, _configuration.BotToken).ConfigureAwait(false);
            await _socketClient.StartAsync().ConfigureAwait(false);

            DiscordLog.Connected(_logger);
            connected = true;
        }
        catch { throw; }
        finally
        {
            // If connection did not complete successfully, unwind any partial state to ensure
            // retries do not accumulate subscriptions or leak a started client.
            if (!connected)
            {
                if (Volatile.Read(ref _eventsWired) == 1)
                {
                    _socketClient.MessageReceived -= OnMessageReceivedAsync;
                    Interlocked.Exchange(ref _eventsWired, 0);
                }

                try { await _socketClient.StopAsync().ConfigureAwait(false); } catch { /* best effort */ }
                try { await _socketClient.LogoutAsync().ConfigureAwait(false); } catch { /* best effort */ }
            }
            _lifecycleSemaphore.Release();
        }
    }

    /// <summary>
    /// Sends a text message to the configured channel.
    /// </summary>
    /// <param name="text">The message content to send.</param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        // Volatile.Read provides a cheap, up-to-date disposed check across threads without locking.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(DiscordGatewayConnection));

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Resolve the target channel each send to avoid caching references that may become invalid across reconnects.
        IMessageChannel messageChannel = _socketClient.GetChannel(_configuration.ChannelId) as IMessageChannel
            ?? throw new InvalidOperationException("Configured channel was not found or is not a message channel.");

        await messageChannel.SendMessageAsync(text).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Message event handler that filters by configured channel and emits entries to the inbound channel.
    /// </summary>
    /// <param name="message">The incoming Discord message.</param>
    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        // Quick checks to drop messages for other channels or after disposal without additional work.
        // Volatile.Read ensures event handler observes latest disposal state to quietly drop messages during shutdown.
        if (message.Channel is null || message.Channel.Id != _configuration.ChannelId || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        DiscordIncoming incoming = new(
            Sender: message.Author?.Username ?? message.Author?.Id.ToString(CultureInfo.InvariantCulture) ?? "unknown",
            Text: message.Content ?? string.Empty,
            MessageId: message.Id.ToString(CultureInfo.InvariantCulture),
            Timestamp: message.Timestamp);

        // Channel writes must tolerate shutdown races. The writer may be completed by the session disposing
        // the gateway or the factory unwinding a failed connect. We prefer TryWrite to avoid awaiting during
        // event dispatch, and fall back to WriteAsync for backpressure if the buffer is full.
        try
        {
            if (!_inboundWriter.TryWrite(incoming))
            {
                await _inboundWriter.WriteAsync(incoming).ConfigureAwait(false);
            }
            DiscordLog.InboundMessage(_logger, incoming.MessageId, incoming.Sender);
        }
        catch (ChannelClosedException)
        {
            // Expected during shutdown; log at Information for visibility and suppress the exception to
            // avoid surfacing as unhandled from event callbacks.
            DiscordLog.InboundChannelClosed(_logger);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested (for example, during shutdown); log for visibility.
            DiscordLog.InboundOperationCanceled(_logger);
        }
    }

    /// <summary>
    /// Unsubscribes handlers, completes inbound channel, attempts orderly gateway shutdown, and disposes the client.
    /// Idempotent and safe to call concurrently with ConnectAsync due to the lifecycle semaphore.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Interlocked.Exchange gates disposal so only one caller runs the critical section.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
        await _lifecycleSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Interlocked.Exchange(ref _eventsWired, 0) == 1)
            {
                _socketClient.MessageReceived -= OnMessageReceivedAsync;
            }

            // Completing the writer wakes any readers awaiting inbound messages.
            _inboundWriter.TryComplete();

            try { await _socketClient.LogoutAsync().ConfigureAwait(false); } catch { /* best effort */ }
            try { await _socketClient.StopAsync().ConfigureAwait(false); } catch { /* best effort */ }
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
