// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIScrivener : IScrivener<OpenAIEntry>
{
    private readonly IScrivener<OpenAIEntry> _inner;
    private readonly IOpenAIGatewayConnection _gateway;
    private readonly ILogger _logger;

    public OpenAIScrivener(
        [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> inner,
        IOpenAIGatewayConnection gateway,
        ILogger<OpenAIScrivener> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _inner = inner;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<long> WriteAsync(OpenAIEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is OpenAIEfferent outgoing)
        {
            await _gateway.SendAsync(outgoing, cancellationToken).ConfigureAwait(false);
        }

        long pos = await _inner.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        OpenAILog.OpenAIScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }

    public IAsyncEnumerable<(long journalPosition, OpenAIEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
        => _inner.TailAsync(afterPosition, cancellationToken);

    public IAsyncEnumerable<(long journalPosition, OpenAIEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => _inner.ReadBackwardAsync(beforePosition, cancellationToken);

    public Task<(long journalPosition, OpenAIEntry entry)> WaitForAsync(long afterPosition, Func<OpenAIEntry, bool> match, CancellationToken cancellationToken = default)
        => _inner.WaitForAsync(afterPosition, match, cancellationToken);

    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default)
        where TDerived : OpenAIEntry
        => _inner.WaitForAsync(afterPosition, match, cancellationToken);
}
