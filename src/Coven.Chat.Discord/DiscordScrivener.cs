using Coven.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Chat.Discord;

internal sealed class DiscordScrivener : IScrivener<DiscordEntry>
{
    private readonly IScrivener<DiscordEntry> _scrivener;
    private readonly DiscordGatewayConnection _discordClient;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="scrivener"></param>
    /// <param name="discordClient"></param>
    /// <remarks> Because we are what we utilize, ensure that the inner scrivener is keyed in DI.</remarks>
    public DiscordScrivener([FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener, DiscordGatewayConnection discordClient)
    {
        ArgumentNullException.ThrowIfNull(scrivener);
        ArgumentNullException.ThrowIfNull(discordClient);
        _scrivener = scrivener;
        _discordClient = discordClient;
    }

    public async Task<long> WriteAsync(DiscordEntry entry, CancellationToken cancellationToken = default)
    {
        // First we write to discord. We are intentionally not solving the problem where we receive an echo of the message we sent.
        await _discordClient.SendAsync(entry.Text, cancellationToken).ConfigureAwait(false);

        // Then we write to our underlying scrivener to ensure that the message is available.
        return await _scrivener.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
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
