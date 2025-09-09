using Coven.Core;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;

namespace Coven.Toys.ConsoleAgentChat;

internal sealed class AgentUser : MagikUser<Empty, string, Guidebook, Spellbook, Testbook>
{
    private readonly ICovenAgent<ChatEntry> _agent;

    public AgentUser(Guidebook guidebook, Spellbook spellbook, Testbook testbook, ICovenAgent<ChatEntry> agent)
        : base(guidebook, spellbook, testbook)
    {
        _agent = agent;
    }

    protected override async Task<string> InvokeMagik(
        Empty input,
        Guidebook guidebook,
        Spellbook spellbook,
        Testbook testbook)
    {
        // No spells needed; just start the agent
        await _agent.InvokeAgent().ConfigureAwait(false);
        return "agent-started";
    }
}
