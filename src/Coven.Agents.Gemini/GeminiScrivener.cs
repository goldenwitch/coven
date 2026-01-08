// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

/// <summary>
/// Gemini scrivener wrapper that forwards outbound efferent entries to the Gemini gateway
/// and persists all entries to the inner journal; logs the append for observability.
/// </summary>
internal sealed class GeminiScrivener : TappedScrivener<GeminiEntry>
{
    private readonly IGeminiGatewayConnection _gateway;
    private readonly ILogger _logger;

    public GeminiScrivener(
        [FromKeyedServices("Coven.InternalGeminiScrivener")] IScrivener<GeminiEntry> inner,
        IGeminiGatewayConnection gateway,
        ILogger<GeminiScrivener> logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    public override async Task<long> WriteAsync(GeminiEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is GeminiEfferent outgoing)
        {
            await _gateway.SendAsync(outgoing, cancellationToken).ConfigureAwait(false);
        }

        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        GeminiLog.GeminiScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
