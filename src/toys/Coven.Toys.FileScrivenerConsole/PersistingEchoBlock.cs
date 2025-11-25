// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.FileScrivenerConsole;

internal sealed class PersistingEchoBlock(IEnumerable<ContractDaemon> daemons, IScrivener<ChatEntry> chat)
    : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _chat = chat ?? throw new ArgumentNullException(nameof(chat));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // Start all registered daemons: console gateway and file flusher
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
        }

        // Echo inbound console messages to outbound chat; file flusher persists the journal
        await foreach ((long _, ChatEntry entry) in _chat.TailAsync(0, cancellationToken))
        {
            if (entry is ChatAfferent r)
            {
                await _chat.WriteAsync(new ChatEfferent("BOT", "Echo: " + r.Text), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return input;
    }
}

