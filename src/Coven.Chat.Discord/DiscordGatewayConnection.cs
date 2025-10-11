using System.Globalization;
using Coven.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordGatewayConnection(
    DiscordClientConfig configuration,
    DiscordSocketClient socketClient,
    [FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener,
    ILogger<DiscordGatewayConnection> logger,
    CancellationToken sessionToken) : IDisposable
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly DiscordSocketClient _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly IScrivener<DiscordEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    public async Task ConnectAsync()
    {
        DiscordLog.Connecting(_logger, _configuration.ChannelId);
        _sessionToken.ThrowIfCancellationRequested();

        _socketClient.MessageReceived += OnMessageReceivedAsync;

        await _socketClient.LoginAsync(TokenType.Bot, _configuration.BotToken).ConfigureAwait(false);
        await _socketClient.StartAsync().ConfigureAwait(false);

        DiscordLog.Connected(_logger);
    }

    public async Task SendAsync(string text)
    {
        // Early abort on session cancellation.
        _sessionToken.ThrowIfCancellationRequested();

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
                RequestOptions requestOptions = new() { CancelToken = _sessionToken };
                IChannel restChannel = await _socketClient.Rest.GetChannelAsync(_configuration.ChannelId, requestOptions);

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

        _sessionToken.ThrowIfCancellationRequested();

        DiscordLog.OutboundSendStart(_logger, _configuration.ChannelId, text.Length);
        // WaitAsync is used to attach the provided cancellation token to the send operation so callers can
        // cooperatively cancel outbound messages, avoiding hangs during shutdown.
        try
        {
            await messageChannel.SendMessageAsync(text).WaitAsync(_sessionToken).ConfigureAwait(false);
            DiscordLog.OutboundSendSucceeded(_logger, _configuration.ChannelId);
        }
        catch (OperationCanceledException)
        {
            DiscordLog.OutboundOperationCanceled(_logger, _configuration.ChannelId);
            throw;
        }
        catch (ObjectDisposedException ex)
        {
            DiscordLog.OutboundSendFailed(_logger, _configuration.ChannelId, ex);
            throw;
        }
        catch (Exception ex)
        {
            DiscordLog.OutboundSendFailed(_logger, _configuration.ChannelId, ex);
            throw;
        }
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        // Determine the sender identity. For Discord.Net, Author should be present on normal messages;
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

        // Just need to send the incoming message to a scrivener
        // Scrivener is responsible for synchronizing etc
        await _scrivener.WriteAsync(incoming);
    }

    public void Dispose()
    {
        _socketClient.MessageReceived -= OnMessageReceivedAsync;
        GC.SuppressFinalize(this);
    }
}
