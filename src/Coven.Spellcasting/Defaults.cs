namespace Coven.Spellcasting;

using System.Threading;
using System.Threading.Tasks;

public sealed record DefaultGuide(string Markdown = "# Guidebook\nFollow user intent; be safe and concise.");

public sealed record DefaultSpell(string Version = "0.1");

public sealed record DefaultTest(string Suite = "smoke");

internal sealed class DefaultGuideFactory<TIn> : IGuidebookFactory<TIn, DefaultGuide>
{
    public Task<Guidebook<DefaultGuide>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Guidebook<DefaultGuide>(new DefaultGuide()));
}

internal sealed class DefaultSpellFactory<TIn> : ISpellbookFactory<TIn, DefaultSpell>
{
    public Task<Spellbook<DefaultSpell>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Spellbook<DefaultSpell>(new DefaultSpell()));
}

internal sealed class DefaultTestFactory<TIn> : ITestbookFactory<TIn, DefaultTest>
{
    public Task<Testbook<DefaultTest>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Testbook<DefaultTest>(new DefaultTest()));
}

