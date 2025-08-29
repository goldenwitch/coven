namespace Coven.Spellcasting.Agents;

using System.Threading;
using System.Threading.Tasks;

public interface ICovenAgent<TIn, TOut>
{
    string Id { get; }

    Task<TOut> CastSpellAsync(
        TIn input,
        SpellContext? context = null,
        CancellationToken ct = default);
}

