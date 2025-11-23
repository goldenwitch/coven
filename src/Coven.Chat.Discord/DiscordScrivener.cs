// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

/// <summary>
/// Discord chat scrivener wrapper that forwards outbound efferent entries to Discord via the gateway
/// and persists all entries to the inner journal so pumps/tests can observe ordering.
/// </summary>
internal sealed class DiscordScrivener : TappedScrivener<DiscordEntry>
{
    private readonly DiscordGatewayConnection _discordClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Wraps a keyed inner scrivener and forwards outbound efferent messages to Discord.
    /// </summary>
    /// <param name="scrivener">The keyed inner scrivener used for storage.</param>
    /// <param name="discordClient">The gateway connection for sending messages to Discord.</param>
    /// <param name="logger">Logger for diagnostic breadcrumbs.</param>
    /// <remarks>Ensure the inner scrivener is keyed in DI.</remarks>
    public DiscordScrivener([FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener, DiscordGatewayConnection discordClient, ILogger<DiscordScrivener> logger)
        : base(scrivener)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends <see cref="DiscordEfferent"/> entries to Discord via the gateway and appends
    /// all entries to the inner scrivener; logs the append with the assigned position.
    /// </summary>
    public override async Task<long> WriteAsync(DiscordEntry entry, CancellationToken cancellationToken = default)
    {
        // Only send actual outbound messages to Discord. ACKs and inbound entries must not be sent.
        if (entry is DiscordEfferent)
        {
            await _discordClient.SendAsync(entry.Text, cancellationToken).ConfigureAwait(false);
        }

        // Always persist to the underlying scrivener so pumps and tests can observe ordering.
        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        DiscordLog.DiscordScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
