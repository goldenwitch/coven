// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Ui;

internal sealed class UiChatGatewayConnection(
    IUiChannel channel,
    UiChatClientConfig configuration,
    [FromKeyedServices("Coven.InternalUiChatScrivener")] IScrivener<UiChatEntry> scrivener,
    ILogger<UiChatGatewayConnection> logger)
{
    private readonly IUiChannel _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    private readonly UiChatClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly IScrivener<UiChatEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private Task? _inputPump;

    /// <summary>
    /// The input pump, so the session supervises it alongside its journal pumps.
    /// </summary>
    /// <remarks>
    /// Left out, a fault in <see cref="IUiChannel.ReadInputAsync"/> or in the journal write
    /// below kills the input path while the daemon stays Running: the interface accepts
    /// messages that go nowhere, and the fault only surfaces during shutdown when
    /// <see cref="DrainAsync"/> finally observes it.
    /// </remarks>
    internal Task Completion => _inputPump ?? Task.CompletedTask;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _inputPump = Task.Run(async () =>
        {
            CancellationToken ct = cancellationToken;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                string? text;
                try
                {
                    text = await _channel.ReadInputAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (text is null)
                {
                    // Channel closed; end the pump cooperatively.
                    break;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string sender = _configuration.InputSender;
                UiChatLog.InboundReceived(_logger, sender, text.Length);

                UiChatAfferent afferent = new(sender, text);
                long position = await _scrivener.WriteAsync(afferent, ct).ConfigureAwait(false);
                UiChatLog.InboundAppended(_logger, nameof(UiChatAfferent), position);
            }
        }, cancellationToken);

        UiChatLog.Connected(_logger);
        return Task.CompletedTask;
    }

    public async Task SendAsync(UiOutboundKind kind, string sender, string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Everything is published, including an empty finalized message. Dropping those here
        // looks tidy but strands the UI: an empty response is exactly what a stream that
        // produced no content finalizes to, and swallowing it leaves the user waiting forever
        // with no way to tell a stalled turn from a slow one. Rendering is the UI's decision.
        await _channel.PublishAsync(new UiOutbound(kind, sender, text), cancellationToken).ConfigureAwait(false);
        UiChatLog.OutboundPublished(_logger, kind.ToString(), text.Length);
    }

    public async Task DrainAsync()
    {
        if (_inputPump is not null)
        {
            try
            {
                await _inputPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cooperative cancellation.
            }
            catch (Exception)
            {
                // Already reported through the session's supervision, which observes this
                // pump while the daemon is running. Re-throwing here would turn a fault that
                // has been handled into a second failure out of disposal.
            }
        }
    }
}
