using Coven.Core;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Chat;

namespace Coven.Toys.CodexConsole;

internal sealed class CodexWizard : MagikUser<Empty, Empty, Guidebook, Spellbook, Testbook>
{
    private readonly ICovenAgent<ChatEntry> _agent;

    public CodexWizard(Guidebook guidebook, Spellbook spellbook, Testbook testbook, ICovenAgent<ChatEntry> agent)
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
        if (spellbook.Definitions is not null)
        {
            await _agent.RegisterSpells(spellbook.Definitions.ToList()).ConfigureAwait(false);
        }

        await _agent.InvokeAgent().ConfigureAwait(false);
        return Empty.Value;
    }
}

