// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Chat;
using Coven.Spellcasting.Spells;

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
        Testbook testbook,
        CancellationToken cancellationToken = default)
    {
        var contracts = spellbook.Spells.OfType<ISpellContract>().ToList();
        if (contracts.Count != 0)
        {
            await _agent.RegisterSpells(contracts, cancellationToken).ConfigureAwait(false);
        }

        await _agent.InvokeAgent(cancellationToken).ConfigureAwait(false);
        return Empty.Value;
    }
}
