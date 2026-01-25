// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Production implementation of <see cref="IDiscordGateway"/> using Discord.Net's
/// <see cref="DiscordSocketClient"/>. Converts the event-based message model to
/// <see cref="IAsyncEnumerable{T}"/> and handles channel resolution internally.
/// </summary>
internal sealed class DiscordNetGateway : IDiscordGateway
{
    private readonly DiscordClientConfig _config;
    private readonly DiscordSocketClient _socketClient;
    private readonly DiscordGatewayOptions _options;
    private readonly ILogger<DiscordNetGateway> _logger;

    // Channel bridges event callbacks to async enumerable
    private readonly Channel<DiscordInboundMessage> _inboundChannel;
    private bool _connected;

    public DiscordNetGateway(
        DiscordClientConfig config,
        DiscordSocketClient socketClient,
        DiscordGatewayOptions options,
        ILogger<DiscordNetGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _inboundChannel = Channel.CreateUnbounded<DiscordInboundMessage>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connected)
        {
            throw new InvalidOperationException("Gateway is already connected.");
        }

        DiscordLog.Connecting(_logger, _config.ChannelId);
        cancellationToken.ThrowIfCancellationRequested();

        _socketClient.MessageReceived += OnMessageReceivedAsync;

        await _socketClient.LoginAsync(TokenType.Bot, _config.BotToken).ConfigureAwait(false);
        await _socketClient.StartAsync().ConfigureAwait(false);

        _connected = true;
        DiscordLog.Connected(_logger);
    }

    public async IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (DiscordInboundMessage message in _inboundChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public async Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        IMessageChannel messageChannel = await ResolveChannelAsync(channelId, cancellationToken)
            .ConfigureAwait(false);

        DiscordLog.OutboundSendStart(_logger, channelId, content.Length);

        try
        {
            await messageChannel.SendMessageAsync(content)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            DiscordLog.OutboundSendSucceeded(_logger, channelId);
        }
        catch (OperationCanceledException)
        {
            DiscordLog.OutboundOperationCanceled(_logger, channelId);
            throw;
        }
        catch (Exception ex)
        {
            DiscordLog.OutboundSendFailed(_logger, channelId, ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _socketClient.MessageReceived -= OnMessageReceivedAsync;
        _inboundChannel.Writer.TryComplete();

        if (_connected)
        {
            await _socketClient.StopAsync().ConfigureAwait(false);
            await _socketClient.LogoutAsync().ConfigureAwait(false);
        }
    }

    private Task OnMessageReceivedAsync(SocketMessage message)
    {
        // Apply bot filtering unless explicitly included
        if (message.Author.IsBot && !_options.IncludeBotMessages)
        {
            DiscordLog.InboundBotMessageObserved(_logger, message.Author.Username,
                message.Content?.Length ?? 0);
            return Task.CompletedTask;
        }

        // Apply channel filter if configured
        if (_options.ChannelFilter is not null &&
            !_options.ChannelFilter.Contains(message.Channel.Id))
        {
            return Task.CompletedTask;
        }

        DiscordInboundMessage inbound = new(
            ChannelId: message.Channel.Id,
            Author: message.Author.Username,
            Content: message.Content ?? string.Empty,
            MessageId: message.Id.ToString(CultureInfo.InvariantCulture),
            Timestamp: message.Timestamp,
            IsBot: message.Author.IsBot);

        DiscordLog.InboundUserMessageReceived(_logger, inbound.Author, inbound.Content.Length);

        // Non-blocking write; channel is unbounded
        _inboundChannel.Writer.TryWrite(inbound);

        return Task.CompletedTask;
    }

    private async Task<IMessageChannel> ResolveChannelAsync(
        ulong channelId,
        CancellationToken cancellationToken)
    {
        // Cache-first lookup
        if (_socketClient.GetChannel(channelId) is IMessageChannel cachedChannel)
        {
            DiscordLog.ChannelCacheHit(_logger, channelId);
            return cachedChannel;
        }

        // REST fallback for cold cache
        DiscordLog.ChannelCacheMiss(_logger, channelId);
        DiscordLog.ChannelRestFetchStart(_logger, channelId);

        try
        {
            RequestOptions requestOptions = new() { CancelToken = cancellationToken };
            IChannel restChannel = await _socketClient.Rest
                .GetChannelAsync(channelId, requestOptions)
                .ConfigureAwait(false);

            if (restChannel is IMessageChannel resolvedChannel)
            {
                DiscordLog.ChannelRestFetchSuccess(_logger, channelId);
                return resolvedChannel;
            }

            string actualType = restChannel?.GetType().Name ?? "null";
            DiscordLog.ChannelRestFetchInvalidType(_logger, channelId, actualType);
            throw new InvalidOperationException(
                $"Channel '{channelId}' is not a message channel (actual: {actualType}).");
        }
        catch (OperationCanceledException)
        {
            DiscordLog.ChannelLookupCanceled(_logger, channelId);
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            DiscordLog.ChannelLookupError(_logger, channelId, ex);
            throw;
        }
    }
}
