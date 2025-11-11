// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.ConsoleChat;

internal sealed class EchoBlock(ContractDaemon consoleDaemon, IScrivener<ChatEntry> scrivener) : IMagikBlock<Empty, Empty>
{
    private readonly ContractDaemon _consoleDaemon = consoleDaemon ?? throw new ArgumentNullException(nameof(consoleDaemon));
    private readonly IScrivener<ChatEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        await _consoleDaemon.Start(cancellationToken);

        await foreach ((long _, ChatEntry? entry) in _scrivener.TailAsync(0, cancellationToken))
        {
            if (entry is ChatAfferent r)
            {
                await _scrivener.WriteAsync(new ChatEfferent("BOT", "Echo: " + r.Text), cancellationToken);
            }
        }

        return input;
    }
}

