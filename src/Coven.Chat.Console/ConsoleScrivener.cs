// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

/// <summary>
/// Console chat scrivener wrapper that forwards outbound efferent entries to the console gateway
/// and persists all entries to the inner journal for deterministic ordering and observation.
/// </summary>
internal sealed class ConsoleScrivener : TappedScrivener<ConsoleEntry>
{
    private readonly ConsoleGatewayConnection _gateway;
    private readonly ILogger _logger;

    public ConsoleScrivener(
        [FromKeyedServices("Coven.InternalConsoleScrivener")] IScrivener<ConsoleEntry> scrivener,
        ConsoleGatewayConnection gateway,
        ILogger<ConsoleScrivener> logger)
        : base(scrivener)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>
    /// Sends <see cref="ConsoleEfferent"/> entries to the console gateway and appends
    /// all entries to the inner scrivener; logs the append with the assigned position.
    /// </summary>
    public override async Task<long> WriteAsync(ConsoleEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is ConsoleEfferent efferent)
        {
            await _gateway.SendAsync(efferent.Text, cancellationToken).ConfigureAwait(false);
        }

        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        ConsoleLog.ConsoleScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
