// SPDX-License-Identifier: BUSL-1.1

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
        var contracts = spellbook.Spells.OfType<Coven.Spellcasting.Spells.ISpellContract>().ToList();
        if (contracts.Count != 0)
        {
            await _agent.RegisterSpells(contracts).ConfigureAwait(false);
        }

        await _agent.InvokeAgent().ConfigureAwait(false);
        return Empty.Value;
    }
}
