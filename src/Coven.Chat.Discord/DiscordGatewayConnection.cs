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

    // Removed standalone disposed flag; disposal is represented by ConnectionState.Disposed.

    // Removed per explicit connection state: event wiring is tied to state transitions (Disconnected -> Connecting).

    /// <summary>
    /// Connection lifecycle state for observability and coordination across threads.
    /// Backed by an <see cref="int"/> field to allow atomic transitions via <see cref="Interlocked"/>.
    /// </summary>
    private enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disposed = 3
    }

    // Backing field storing the current connection state; use Interlocked for writes and Volatile for reads.
    private int _connectionState = (int)ConnectionState.Disconnected;

    // Readiness signaling for the current connection attempt. Always non-null; replaced per connect.
    // TaskCompletionSource uses RunContinuationsAsynchronously so continuations do not run inline on the event thread.
    private TaskCompletionSource<bool> _clientReadyCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Cancellation token source used to cooperatively cancel in-flight operations (e.g., channel writes)
    // during shutdown. This avoids indefinite blocking under backpressure and ensures responsive teardown.
    private CancellationTokenSource _shutdownCancellationSource = new();
    // Cached token to avoid reading from a potentially disposed source during races.
    private CancellationToken _shutdownCancellationToken;

    /// <summary>
    /// Handles the Discord client's Ready event by completing the readiness signal for the current connection attempt.
    /// </summary>
    /// <remarks>
    /// TrySetResult tolerates duplicate or late events and races with cleanup. We return a completed task to
    /// conform to the expected event delegate signature without blocking the event thread.
    /// </remarks>
    private Task OnClientReady()
    {
        // The volatile field is only written while holding the lifecycle semaphore; here we only read and attempt completion.
        // TrySetResult tolerates duplicate events and races with cleanup without throwing.
        _clientReadyCompletionSource.TrySetResult(true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Connects to Discord, authenticates the bot, starts the gateway, wires message handlers,
    /// and awaits the client readiness signal before returning. Idempotent with respect to event
    /// wiring: only the first call will subscribe persistent handlers.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the connection attempt.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Volatile.Read provides an up-to-date state check across threads without locking.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _connectionState) == (int)ConnectionState.Disposed, nameof(DiscordGatewayConnection));

        // Ensure only one concurrent ConnectAsync or DisposeAsync runs at a time.
        await _lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool connected = false;
        try
        {
            // Atomic state transition so other threads (e.g., event handlers) can observe we are connecting.
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connecting);
            DiscordLog.Connecting(_logger, _configuration.ChannelId);

            // Wire events when beginning a connection attempt. Because we hold the lifecycle semaphore,
            // upstream cannot concurrently wire again. Removing a handler that may not be subscribed is a no-op
            // during unwind/dispose, so we do not need an additional flag.
            _socketClient.MessageReceived += OnMessageReceivedAsync;

            // Initialize the readiness signal and subscribe to the canonical Ready handler for this connection attempt.
            // Subscribing before starting avoids missing a fast-fired Ready event after Login/Start.
            _clientReadyCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // Initialize the shutdown CTS for this connected lifetime. It is canceled in DisposeAsync/unwind paths.
            _shutdownCancellationSource = new CancellationTokenSource();
            _shutdownCancellationToken = _shutdownCancellationSource.Token;
            _socketClient.Ready += OnClientReady;

            await _socketClient.LoginAsync(TokenType.Bot, _configuration.BotToken).ConfigureAwait(false);
            await _socketClient.StartAsync().ConfigureAwait(false);

            // Await the client readiness signal so that callers observe a truly ready connection.
            // WaitAsync honors cancellation so shutdown requests propagate promptly.
            Task readinessTask = _clientReadyCompletionSource.Task;
            await readinessTask.WaitAsync(cancellationToken).ConfigureAwait(false);

            DiscordLog.Connected(_logger);
            connected = true;
            // Atomic state transition to Connected after readiness to reflect a fully usable client.
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
        }
        catch { throw; }
        finally
        {
            // Always unsubscribe the Ready handler to avoid event leaks and clear the readiness signal reference.
            try { _socketClient.Ready -= OnClientReady; } catch { /* best effort */ }

            // If connection did not complete successfully, unwind any partial state to ensure
            // retries do not accumulate subscriptions or leak a started client.
            if (!connected)
            {
                // Ensure external observers see a Disconnected state after unwind.
                Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
                // Cancel any in-flight operations tied to this connection attempt to avoid hangs.
                try { _shutdownCancellationSource.Cancel(); } catch { /* best effort */ }
                _shutdownCancellationSource.Dispose();
                // Unsubscribe the message handler; removing a non-subscribed handler is a no-op.
                _socketClient.MessageReceived -= OnMessageReceivedAsync;

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
        // Volatile.Read ensures we observe the latest connection state across threads without locking.
        // We throw ObjectDisposedException when the connection object lifetime has ended.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _connectionState) == (int)ConnectionState.Disposed, nameof(DiscordGatewayConnection));

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Resolve the target channel each send to avoid caching references that may become invalid across reconnects.
        // Attempt cache-first, then fall back to REST with cooperative cancellation for resilience on cold caches.
        IMessageChannel messageChannel;
        if (_socketClient.GetChannel(_configuration.ChannelId) is not IMessageChannel cachedMessageChannel)
        {
            DiscordLog.ChannelCacheMiss(_logger, _configuration.ChannelId);
            try
            {
                DiscordLog.ChannelRestFetchStart(_logger, _configuration.ChannelId);
                // RequestOptions carries the cancellation token so REST calls honor cooperative cancellation.
                RequestOptions requestOptions = new() { CancelToken = cancellationToken };
                IChannel? restChannel = await _socketClient.Rest.GetChannelAsync(_configuration.ChannelId, requestOptions).ConfigureAwait(false);
                if (restChannel is IMessageChannel resolvedMessageChannel)
                {
                    messageChannel = resolvedMessageChannel;
                    DiscordLog.ChannelRestFetchSuccess(_logger, _configuration.ChannelId);
                }
                else
                {
                    // Provide clear diagnostics when the resolved channel is not a message-capable channel.
                    string actualTypeName = restChannel?.GetType().Name ?? "null";
                    DiscordLog.ChannelRestFetchInvalidType(_logger, _configuration.ChannelId, actualTypeName);
                    throw new InvalidOperationException($"Configured channel '{_configuration.ChannelId}' resolved via REST but is not a message channel (actual: {actualTypeName}).");
                }
            }
            catch (OperationCanceledException)
            {
                // Await cancellation with context for observability; rethrow to propagate to caller.
                DiscordLog.ChannelLookupCanceled(_logger, _configuration.ChannelId);
                throw;
            }
            catch (Exception error)
            {
                // Log detailed lookup failure with the channel identifier for triage, then rethrow.
                DiscordLog.ChannelLookupError(_logger, _configuration.ChannelId, error);
                throw;
            }
        }
        else
        {
            DiscordLog.ChannelCacheHit(_logger, _configuration.ChannelId);
            messageChannel = cachedMessageChannel;
        }

        // WaitAsync is used to attach the provided cancellation token to the send operation so callers can
        // cooperatively cancel outbound messages, avoiding hangs during shutdown.
        await messageChannel.SendMessageAsync(text).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Message event handler that filters by configured channel and emits entries to the inbound channel.
    /// </summary>
    /// <param name="message">The incoming Discord message.</param>
    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        // Quick checks to drop messages for other channels or after disposal without additional work.
        // Volatile.Read ensures the event handler observes the latest state to quietly drop messages during shutdown.
        if (message.Channel is null || message.Channel.Id != _configuration.ChannelId || Volatile.Read(ref _connectionState) == (int)ConnectionState.Disposed)
        {
            return;
        }

        // Determine the sender identity. For Discord.Net, Author should be present on normal messages;
        // we do not silently substitute identifiers here to avoid surprising behavior.
        string sender = message.Author.Username;
        if (string.IsNullOrEmpty(sender))
        {
            throw new InvalidOperationException("Discord message author username is missing.");
        }

        DiscordIncoming incoming = new(
            Sender: sender,
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
                // Await with a shutdown-aware token so the event thread does not block indefinitely under backpressure.
                // The token is canceled during DisposeAsync and connection-unwind paths.
                // Use the cached token captured when the CTS was created to avoid touching a potentially disposed source.
                CancellationToken shutdownToken = _shutdownCancellationToken;
                await _inboundWriter.WriteAsync(incoming, shutdownToken).ConfigureAwait(false);
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
        // Atomic state transition ensures only the first caller performs disposal. Subsequent callers return immediately.
        int previousState = Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disposed);
        if (previousState == (int)ConnectionState.Disposed)
        {
            return;
        }
        await _lifecycleSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Cancel any in-flight operations (e.g., channel writes) to ensure a prompt and orderly shutdown.
            try { _shutdownCancellationSource.Cancel(); } catch { /* best effort */ }
            // Unsubscribe regardless; removing a non-subscribed handler is a no-op.
            _socketClient.MessageReceived -= OnMessageReceivedAsync;

            // Completing the writer wakes any readers awaiting inbound messages.
            _inboundWriter.TryComplete();

            try { await _socketClient.LogoutAsync().ConfigureAwait(false); } catch { /* best effort */ }
            try { await _socketClient.StopAsync().ConfigureAwait(false); } catch { /* best effort */ }
            _socketClient.Dispose();
            _shutdownCancellationSource.Dispose();

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
