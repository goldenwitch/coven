// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Claude;

/// <summary>
/// Claude scrivener wrapper that forwards outbound efferent entries to the Claude gateway
/// and persists all entries to the inner journal; logs the append for observability.
/// </summary>
internal sealed class ClaudeScrivener : TappedScrivener<ClaudeEntry>
{
    private readonly IClaudeGatewayConnection _gateway;
    private readonly ILogger _logger;

    public ClaudeScrivener(
        [FromKeyedServices("Coven.InternalClaudeScrivener")] IScrivener<ClaudeEntry> inner,
        IClaudeGatewayConnection gateway,
        ILogger<ClaudeScrivener> logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    public override async Task<long> WriteAsync(ClaudeEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is ClaudeEfferent outgoing)
        {
            await _gateway.SendAsync(outgoing, cancellationToken).ConfigureAwait(false);
        }

        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        ClaudeLog.ClaudeScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
