namespace Coven.Spellcasting;

using System.Threading;
using System.Threading.Tasks;

public interface IGuidebookFactory<TIn, TGuide>
{
    Task<IBook<TGuide>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ISpellbookFactory<TIn, TSpell>
{
    Task<IBook<TSpell>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ITestbookFactory<TIn, TTest>
{
    Task<IBook<TTest>> CreateAsync(TIn input, CancellationToken ct);
}
