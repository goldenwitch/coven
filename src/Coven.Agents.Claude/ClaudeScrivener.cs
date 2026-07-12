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
    private readonly ILogger _logger;

    public ClaudeScrivener(
        [FromKeyedServices("Coven.InternalClaudeScrivener")] IScrivener<ClaudeEntry> inner,
        ILogger<ClaudeScrivener> logger)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public override async Task<long> WriteAsync(ClaudeEntry entry, CancellationToken cancellationToken = default)
    {
        long pos = await WriteInnerAsync(entry, cancellationToken).ConfigureAwait(false);
        ClaudeLog.ClaudeScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }
}
