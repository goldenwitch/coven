// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

/// <summary>
/// OpenAI scrivener wrapper that forwards outbound efferent entries to the OpenAI gateway
/// and persists all entries to the inner journal; logs the append for observability.
/// </summary>
internal sealed class OpenAIScrivener : TappedScrivener<OpenAIEntry>
{
    private readonly IOpenAIGatewayConnection _gateway;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates an instance wrapping a keyed inner scrivener and an OpenAI gateway connection.
    /// </summary>
    /// <param name="inner">The keyed inner scrivener used for storage.</param>
    /// <param name="gateway">Gateway connection for sending OpenAI requests.</param>
    /// <param name="logger">Logger for diagnostic breadcrumbs.</param>
    public OpenAIScrivener(
        [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> inner,
        IOpenAIGatewayConnection gateway,
        ILogger<OpenAIScrivener> logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>
    /// Sends <see cref="OpenAIEfferent"/> entries via the gateway and appends all entries to the inner scrivener;
    /// logs the append with the assigned position.
    /// </summary>
    public override async Task<long> WriteAsync(OpenAIEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is OpenAIEfferent outgoing)
        {
            await _gateway.SendAsync(outgoing, cancellationToken).ConfigureAwait(false);
        }

        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        OpenAILog.OpenAIScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
