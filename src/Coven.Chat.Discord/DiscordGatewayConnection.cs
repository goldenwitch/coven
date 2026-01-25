// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Bridges <see cref="IDiscordGateway"/> to the Discord journal by pumping inbound
/// messages to the internal scrivener. Outbound sends are delegated to the gateway.
/// </summary>
internal sealed class DiscordGatewayConnection(
    DiscordClientConfig config,
    IDiscordGateway gateway,
    [FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener,
    ILogger<DiscordGatewayConnection> logger) : IAsyncDisposable
{
    private readonly DiscordClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IDiscordGateway _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<DiscordEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly ILogger<DiscordGatewayConnection> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private Task? _inboundPump;
    private CancellationTokenSource? _pumpCts;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _gateway.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inboundPump = PumpInboundMessagesAsync(_pumpCts.Token);
    }

    public Task SendAsync(string text, CancellationToken cancellationToken)
    {
        return _gateway.SendMessageAsync(_config.ChannelId, text, cancellationToken);
    }

    private async Task PumpInboundMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (DiscordInboundMessage message in _gateway.GetInboundMessagesAsync(cancellationToken))
            {
                DiscordAfferent afferent = new(
                    Sender: message.Author,
                    Text: message.Content,
                    MessageId: message.MessageId,
                    Timestamp: message.Timestamp);

                long position = await _scrivener.WriteAsync(afferent, cancellationToken)
                    .ConfigureAwait(false);
                DiscordLog.InboundAppendedToJournal(_logger, nameof(DiscordAfferent), position);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pumpCts is not null)
        {
            try
            {
                _pumpCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // CTS was already disposed (can happen with linked tokens), which is fine
            }
        }

        if (_inboundPump is not null)
        {
            try
            {
                await _inboundPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }

        _pumpCts?.Dispose();
        await _gateway.DisposeAsync().ConfigureAwait(false);
    }
}
