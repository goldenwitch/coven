using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.DiscordChat;

public class EchoBlock(ContractDaemon discordDaemon, IScrivener<ChatEntry> scrivener) : IMagikBlock<Empty, Empty>
{
    private readonly ContractDaemon _discordDaemon = discordDaemon ?? throw new ArgumentNullException(nameof(discordDaemon));
    private readonly IScrivener<ChatEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // Start our contract Daemon
        await _discordDaemon.Start(cancellationToken);

        // Tail our chat scrivener and echo all of the messages to it that aren't sent by us.
        await foreach ((long _, ChatEntry? entry) in _scrivener.TailAsync(0, cancellationToken))
        {
            // We don't need to handle outgoing messages as they represent something we have sent.
            // It's also okay to take the extra cycle to look at them rather than short circuiting, we might use them as confirmation of receipt in the future.
            switch (entry)
            {
                case ChatIncoming r:
                    await _scrivener.WriteAsync(new ChatOutgoing("BOT", r.Text), cancellationToken);
                    break;
                default:
                    break;
            }
        }

        // When we exit the scope, it should automatically cancel and dispose our boys.
        return input;
    }
}
