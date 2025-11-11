// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Chat;
using Coven.Core;
using Coven.Daemonology;

namespace DiscordAgent;

internal sealed class RouterBlock(
    IEnumerable<ContractDaemon> daemons,
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    private readonly IEnumerable<ContractDaemon> _daemons = daemons ?? throw new ArgumentNullException(nameof(daemons));
    private readonly IScrivener<ChatEntry> _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IScrivener<AgentEntry> _agents = agents ?? throw new ArgumentNullException(nameof(agents));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        foreach (ContractDaemon d in _daemons)
        {
            await d.Start(cancellationToken).ConfigureAwait(false);
        }

        Task chatToAgents = Task.Run(async () =>
        {
            await foreach ((long _, ChatEntry? entry) in _chat.TailAsync(0, cancellationToken))
            {
                if (entry is ChatAfferent inc)
                {
                    await _agents.WriteAsync(new AgentPrompt(inc.Sender, inc.Text), cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken);

        Task agentsToChat = Task.Run(async () =>
        {
            await foreach ((long _, AgentEntry? entry) in _agents.TailAsync(0, cancellationToken))
            {
                switch (entry)
                {
                    case AgentResponse r:
                        await _chat.WriteAsync(new ChatEfferentDraft("BOT", r.Text), cancellationToken).ConfigureAwait(false);
                        break;
                    case AgentThought t:
                        // Uncomment below if you want to output thoughts :)
                        // await _chat.WriteAsync(new ChatEfferentDraft("BOT", t.Text), cancellationToken).ConfigureAwait(false);
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
