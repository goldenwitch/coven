// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.DiscordChat;

internal sealed class EchoBlock(IEnumerable<ContractDaemon> daemons, IScrivener<ChatEntry> scrivener) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // Start all contract daemons
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
        }

        // Tail our chat scrivener and echo all of the messages to it that aren't sent by us.
        await foreach ((long _, ChatEntry? entry) in _scrivener.TailAsync(0, cancellationToken))
        {
            // We don't need to handle outgoing messages as they represent something we have sent.
            // It's also okay to take the extra cycle to look at them rather than short circuiting, we might use them as confirmation of receipt in the future.
            switch (entry)
            {
                case ChatAfferent r:
                    await _scrivener.WriteAsync(new ChatEfferent("BOT", r.Text), cancellationToken);
                    break;
                default:
                    break;
            }
        }

        // When we exit the scope, cooperative shutdown disposes daemons.
        return input;
    }
}
