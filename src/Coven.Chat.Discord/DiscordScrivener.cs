using Coven.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordScrivener : IScrivener<DiscordEntry>
{
    private readonly IScrivener<DiscordEntry> _scrivener;
    private readonly DiscordGatewayConnection _discordClient;
    private readonly ILogger _logger;

    /// <summary>
    /// Wraps a keyed inner scrivener and forwards outbound efferent messages to Discord.
    /// </summary>
    /// <param name="scrivener">The keyed inner <see cref="IScrivener{TJournalEntryType}"/> used for storage.</param>
    /// <param name="discordClient">The gateway connection for sending messages to Discord.</param>
    /// <param name="logger">Logger for diagnostic breadcrumbs.</param>
    /// <remarks> Because we are what we utilize, ensure that the inner scrivener is keyed in DI.</remarks>
    public DiscordScrivener([FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener, DiscordGatewayConnection discordClient, ILogger<DiscordScrivener> logger)
    {
        ArgumentNullException.ThrowIfNull(scrivener);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);
        _scrivener = scrivener;
        _discordClient = discordClient;
        _logger = logger;
    }

    public async Task<long> WriteAsync(DiscordEntry entry, CancellationToken cancellationToken = default)
    {
        // Only send actual outbound messages to Discord. ACKs and inbound entries must not be sent.
        if (entry is DiscordEfferent)
        {
            await _discordClient.SendAsync(entry.Text, cancellationToken).ConfigureAwait(false);
        }

        // Always persist to the underlying scrivener so pumps and tests can observe ordering.
        long pos = await _scrivener.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        DiscordLog.DiscordScrivenerAppended(_logger, entry.GetType().Name, pos);
        return pos;
    }

    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
        => _scrivener.TailAsync(afterPosition, cancellationToken);

    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => _scrivener.ReadBackwardAsync(beforePosition, cancellationToken);

    public Task<(long journalPosition, DiscordEntry entry)> WaitForAsync(long afterPosition, Func<DiscordEntry, bool> match, CancellationToken cancellationToken = default)
        => _scrivener.WaitForAsync(afterPosition, match, cancellationToken);

    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default)
        where TDerived : DiscordEntry
        => _scrivener.WaitForAsync(afterPosition, match, cancellationToken);
}
