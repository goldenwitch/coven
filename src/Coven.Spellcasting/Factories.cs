namespace Coven.Spellcasting;

using System.Threading;
using System.Threading.Tasks;

public interface IGuidebookFactory<TIn, TGuide>
{
    Task<Guidebook<TGuide>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ISpellbookFactory<TIn, TSpell>
{
    Task<Spellbook<TSpell>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ITestbookFactory<TIn, TTest>
{
    Task<Testbook<TTest>> CreateAsync(TIn input, CancellationToken ct);
}

