using Coven.Core;
using Coven.Daemonology;

namespace Coven.Chat.Discord;

public class DiscordChatDaemon(IScrivener<DaemonEvent> scrivener) : ContractDaemon(scrivener)
{
    public override Task Shutdown(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task Start(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

