namespace Coven.Spellcasting.Agents;

using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Spells;


public interface ICovenAgent<TIn, TOut>
{
    string Id { get; }

    Task RegisterSpells(List<SpellDefinition> Spells);

    Task<TOut> CastSpell(
        TIn input,
        CancellationToken ct = default);
}

