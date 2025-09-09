using Coven.Core;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Spells;

namespace Coven.Samples.LocalCodexCLI;

internal sealed class Wizard : MagikUser<Empty, string, Guidebook, Spellbook, Testbook>
{
    private readonly ICovenAgent<string> _agent;
    private readonly Spellbook _spellbook;

    public Wizard(Guidebook guidebook, Spellbook spellbook, Testbook testbook, ICovenAgent<string> agent)
        : base(guidebook, spellbook, testbook)
    {
        _agent = agent;
        _spellbook = spellbook;
    }

    protected override async Task<string> InvokeMagik(
        Empty input,
        Guidebook guidebook,
        Spellbook spellbook,
        Testbook testbook)
    {
        // Provide spell definitions to the agent and start it.
        if (spellbook.Definitions is not null)
        {
            await _agent.RegisterSpells(spellbook.Definitions.ToList()).ConfigureAwait(false);
        }

        await _agent.InvokeAgent().ConfigureAwait(false);
        return "wizard-started";
    }

}
