using Coven.Core;
using Coven.Daemonology;

namespace Coven.Chat.Discord;

public class DiscordChatDaemon(IScrivener<DaemonEvent> scrivener) : ContractDaemon(scrivener)
{
    public override Task Shutdown(CancellationToken cancellationToken)
    {
        return Transition(Status.Completed, cancellationToken);
    }

    public override Task Start(CancellationToken cancellationToken)
    {
        return Transition(Status.Running, cancellationToken);
    }
}
