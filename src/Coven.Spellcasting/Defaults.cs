namespace Coven.Spellcasting;

using System.Threading;
using System.Threading.Tasks;

public record DefaultGuide(string Markdown = "# Guidebook\nFollow user intent; be safe and concise.");

public record DefaultSpell(string Version = "0.1");

public record DefaultTest(string Suite = "smoke");

internal sealed class DefaultGuideFactory<TIn> : IGuidebookFactory<TIn, DefaultGuide>
{
    public Task<IBook<DefaultGuide>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult<IBook<DefaultGuide>>(new Guidebook<DefaultGuide>(new DefaultGuide()));
}

internal sealed class DefaultSpellFactory<TIn> : ISpellbookFactory<TIn, DefaultSpell>
{
    public Task<IBook<DefaultSpell>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult<IBook<DefaultSpell>>(new Spellbook<DefaultSpell>(new DefaultSpell()));
}

internal sealed class DefaultTestFactory<TIn> : ITestbookFactory<TIn, DefaultTest>
{
    public Task<IBook<DefaultTest>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult<IBook<DefaultTest>>(new Testbook<DefaultTest>(new DefaultTest()));
}
