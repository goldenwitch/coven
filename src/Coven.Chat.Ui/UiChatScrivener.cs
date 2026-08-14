// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Ui;

/// <summary>
/// UI chat scrivener wrapper that forwards renderable entries to the UI channel and
/// persists all entries to the inner journal for deterministic ordering and observation.
/// </summary>
internal sealed class UiChatScrivener : TappedScrivener<UiChatEntry>
{
    private readonly UiChatGatewayConnection _gateway;
    private readonly ILogger _logger;

    public UiChatScrivener(
        [FromKeyedServices("Coven.InternalUiChatScrivener")] IScrivener<UiChatEntry> scrivener,
        UiChatGatewayConnection gateway,
        ILogger<UiChatScrivener> logger)
        : base(scrivener)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>
    /// Sends renderable entries to the UI channel and appends all entries to the inner
    /// scrivener; logs the append with the assigned position.
    /// </summary>
    public override async Task<long> WriteAsync(UiChatEntry entry, CancellationToken cancellationToken = default)
    {
        switch (entry)
        {
            case UiChatEfferent efferent:
                await _gateway.SendAsync(UiOutboundKind.Message, efferent.Sender, efferent.Text, cancellationToken).ConfigureAwait(false);
                break;

            case UiChatChunk chunk:
                await _gateway.SendAsync(UiOutboundKind.Chunk, chunk.Sender, chunk.Text, cancellationToken).ConfigureAwait(false);
                break;

            default:
                // Acks and submissions are journal-only; nothing to render.
                break;
        }

        long position = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        UiChatLog.ScrivenerAppended(_logger, entry.GetType().Name, position);
        return position;
    }
}
