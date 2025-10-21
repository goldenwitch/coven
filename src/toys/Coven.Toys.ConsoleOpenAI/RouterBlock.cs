using Coven.Agents;
using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace Coven.Toys.ConsoleOpenAI;

public sealed class RouterBlock(
    IEnumerable<ContractDaemon> daemons,
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IScrivener<AgentEntry> _agents = agents ?? throw new ArgumentNullException(nameof(agents));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        // Start all registered daemons (Console + OpenAI)
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
        }

        // Pump 1: Chat -> Agents (prompts)
        Task chatToAgents = Task.Run(async () =>
        {
            await foreach ((long _, ChatEntry? entry) in _chat.TailAsync(0, cancellationToken))
            {
                if (entry is ChatIncoming inc)
                {
                    await _agents.WriteAsync(new AgentPrompt(inc.Sender, inc.Text), cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken);

        // Pump 2: Agents -> Chat (responses)
        Task agentsToChat = Task.Run(async () =>
        {
            await foreach ((long _, AgentEntry? entry) in _agents.TailAsync(0, cancellationToken))
            {
                switch (entry)
                {
                    case AgentResponse r:
                        await _chat.WriteAsync(new ChatOutgoing("BOT", r.Text), cancellationToken).ConfigureAwait(false);
                        break;
                    case AgentThought t:
                        await _chat.WriteAsync(new ChatOutgoing("BOT", t.Text), cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        break;
                }
            }
        }, cancellationToken);

        await Task.WhenAll(chatToAgents, agentsToChat).ConfigureAwait(false);
        return input;
    }
}
