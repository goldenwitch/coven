using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.DiscordStreaming;

public sealed class StreamingBlock(IEnumerable<ContractDaemon> daemons, IScrivener<ChatEntry> scrivener) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // Start all registered daemons: Discord gateway, shattering, and windowing
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
        }

        // Keep the ritual alive by tailing the chat journal.
        // We do not echo manually; shattering + windowing will emit ChatOutgoing automatically.
        await foreach ((long _, ChatEntry _) in _scrivener.TailAsync(0, cancellationToken))
        {
            // No-op: presence loop
        }

        return input;
    }
}

