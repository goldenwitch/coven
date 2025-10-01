using Coven.Core;

namespace Coven.Chat.Discord;

public class DiscordScrivener : IScrivener<DiscordEntry>
{
    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<(long journalPosition, DiscordEntry entry)> WaitForAsync(long afterPosition, Func<DiscordEntry, bool> match, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default) where TDerived : DiscordEntry
    {
        throw new NotImplementedException();
    }

    public Task<long> WriteAsync(DiscordEntry entry, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
