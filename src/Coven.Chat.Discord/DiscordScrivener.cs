using Coven.Core;

namespace Coven.Chat.Discord;

// Live-only scrivener backed by Discord gateway events. No backward reads yet.
internal sealed class DiscordScrivener : IScrivener<DiscordEntry>, IDisposable
{
    private readonly IDiscordClient _client;
    private readonly string _channelId;
    private readonly InMemoryScrivener<DiscordEntry> _memory = new();

    private CancellationTokenSource? _watchCts;
    private Task? _watchTask;

    public DiscordScrivener(IDiscordClient client, DiscordChatAdapterOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ChannelId))
        {
            throw new ArgumentException("ChannelId is required.", nameof(options));
        }

        _channelId = options.ChannelId;
    }

    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<(long journalPosition, DiscordEntry entry)> TailAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
    {
        EnsureWatcherStarted();
        return _memory.TailAsync(afterPosition, cancellationToken);
    }

    public Task<(long journalPosition, DiscordEntry entry)> WaitForAsync(long afterPosition, Func<DiscordEntry, bool> match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        EnsureWatcherStarted();
        return _memory.WaitForAsync(afterPosition, match, cancellationToken);
    }

    public Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default) where TDerived : DiscordEntry
    {
        ArgumentNullException.ThrowIfNull(match);
        EnsureWatcherStarted();
        return _memory.WaitForAsync(afterPosition, match, cancellationToken);
    }

    public async Task<long> WriteAsync(DiscordEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry is DiscordOutgoing o)
        {
            await _client.SendAsync(_channelId, o.Text, cancellationToken).ConfigureAwait(false);
        }
        return await _memory.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureWatcherStarted()
    {
        if (_watchTask is not null)
        {
            return;
        }


        _watchCts = new CancellationTokenSource();
        _watchTask = Task.Run(() => WatchLoop(_watchCts.Token));
    }

    private async Task WatchLoop(CancellationToken ct)
    {
        try
        {
            await foreach (DiscordMessage m in _client.WatchAsync(_channelId, null, ct).ConfigureAwait(false))
            {
                DiscordIncoming incoming = new(m.Author, m.Text, m.Id, m.Timestamp);
                await _memory.WriteAsync(incoming, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _watchCts?.Cancel();
        _watchCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
