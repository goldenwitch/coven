// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Spells;
using Coven.Chat;

namespace Coven.Samples.LocalCodexCLI;

internal sealed class Wizard : MagikUser<Empty, Empty, Guidebook, Spellbook, Testbook>
{
    private readonly ICovenAgent<ChatEntry> _agent;
    private readonly Spellbook _spellbook;

    public Wizard(Guidebook guidebook, Spellbook spellbook, Testbook testbook, ICovenAgent<ChatEntry> agent)
        : base(guidebook, spellbook, testbook)
    {
        _agent = agent;
        _spellbook = spellbook;
    }

    protected override async Task<Empty> InvokeMagik(
        Empty input,
        Guidebook guidebook,
        Spellbook spellbook,
        Testbook testbook)
    {
        // Provide spells to the agent and start it (definitions come from contracts).
        var contracts = spellbook.Spells.OfType<ISpellContract>().ToList();
        if (contracts.Count != 0)
        {
            await _agent.RegisterSpells(contracts).ConfigureAwait(false);
        }

        await _agent.InvokeAgent().ConfigureAwait(false);
        return Empty.Value;
    }
}
