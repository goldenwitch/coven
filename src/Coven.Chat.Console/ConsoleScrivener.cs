using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal sealed class ConsoleScrivener : IScrivener<ConsoleEntry>
{
    private readonly IScrivener<ConsoleEntry> _scrivener;
    private readonly ConsoleGatewayConnection _gateway;
    private readonly ILogger _logger;

    public ConsoleScrivener(
        [FromKeyedServices("Coven.InternalConsoleScrivener")] IScrivener<ConsoleEntry> scrivener,
        ConsoleGatewayConnection gateway,
        ILogger<ConsoleScrivener> logger)
    {
        ArgumentNullException.ThrowIfNull(scrivener);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);
        _scrivener = scrivener;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<long> WriteAsync(ConsoleEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is ConsoleOutgoing)
        {
            await _gateway.SendAsync(entry.Text, cancellationToken).ConfigureAwait(false);
        }

        long pos = await _scrivener.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        ConsoleLog.ConsoleScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }

    public IAsyncEnumerable<(long journalPosition, ConsoleEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
        => _scrivener.TailAsync(afterPosition, cancellationToken);

    public IAsyncEnumerable<(long journalPosition, ConsoleEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => _scrivener.ReadBackwardAsync(beforePosition, cancellationToken);

    public Task<(long journalPosition, ConsoleEntry entry)> WaitForAsync(long afterPosition, Func<ConsoleEntry, bool> match, CancellationToken cancellationToken = default)
        => _scrivener.WaitForAsync(afterPosition, match, cancellationToken);

    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default)
        where TDerived : ConsoleEntry
        => _scrivener.WaitForAsync(afterPosition, match, cancellationToken);
}

