using Coven.Core;
using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;

namespace Coven.Toys.ConsoleAgentChat;

// Minimal MagikUser that simply starts the agent and returns Empty.
internal sealed class AgentUser : MagikUser<Empty, Empty, Guidebook, Spellbook, Testbook>
{
    private readonly ICovenAgent<ChatEntry> _agent;

    public AgentUser(Guidebook guidebook, Spellbook spellbook, Testbook testbook, ICovenAgent<ChatEntry> agent)
        : base(guidebook, spellbook, testbook)
    {
        _agent = agent;
    }

    protected override async Task<Empty> InvokeMagik(
        Empty input,
        Guidebook guidebook,
        Spellbook spellbook,
        Testbook testbook)
    {
        // No spells needed; just start the agent
        await _agent.InvokeAgent().ConfigureAwait(false);
        return Empty.Value;
    }
}
