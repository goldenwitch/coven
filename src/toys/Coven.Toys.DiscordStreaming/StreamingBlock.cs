using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.DiscordStreaming;

internal sealed class StreamingBlock(IEnumerable<ContractDaemon> daemons, IScrivener<ChatEntry> scrivener) : IMagikBlock<Empty, Empty>
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

        // Tail the chat journal and convert incoming to draft outgoing.
        await foreach ((long _, ChatEntry entry) in _scrivener.TailAsync(0, cancellationToken))
        {
            if (entry is ChatAfferent i)
            {
                await _scrivener.WriteAsync(new ChatEfferentDraft("BOT", i.Text), cancellationToken).ConfigureAwait(false);
            }
        }

        return input;
    }
}
