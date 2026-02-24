// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// LLamaSharp scrivener wrapper that forwards outbound efferent entries to the gateway
/// and persists all entries to the inner journal; logs the append for observability.
/// </summary>
internal sealed class LLamaSharpScrivener : TappedScrivener<LLamaSharpEntry>
{
    private readonly ILLamaSharpGatewayConnection _gateway;
    private readonly ILogger _logger;

    public LLamaSharpScrivener(
        [FromKeyedServices("Coven.InternalLLamaSharpScrivener")] IScrivener<LLamaSharpEntry> inner,
        ILLamaSharpGatewayConnection gateway,
        ILogger<LLamaSharpScrivener> logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    public override async Task<long> WriteAsync(LLamaSharpEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is LLamaSharpEfferent outgoing)
        {
            await _gateway.SendAsync(outgoing, cancellationToken).ConfigureAwait(false);
        }

        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        LLamaSharpLog.LLamaSharpScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
