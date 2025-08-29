namespace Coven.Spellcasting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core;

public abstract class MagikUser<TIn, TOut, TGuide, TSpell, TTest> : IMagikBlock<TIn, TOut>
{
    private readonly IGuidebookFactory<TIn, TGuide> _guideFactory;
    private readonly ISpellbookFactory<TIn, TSpell> _spellFactory;
    private readonly ITestbookFactory<TIn, TTest> _testFactory;

    protected MagikUser(
        IGuidebookFactory<TIn, TGuide> guideFactory,
        ISpellbookFactory<TIn, TSpell> spellFactory,
        ITestbookFactory<TIn, TTest> testFactory)
    {
        _guideFactory = guideFactory ?? throw new ArgumentNullException(nameof(guideFactory));
        _spellFactory = spellFactory ?? throw new ArgumentNullException(nameof(spellFactory));
        _testFactory  = testFactory  ?? throw new ArgumentNullException(nameof(testFactory));
    }

    public async Task<TOut> DoMagik(TIn input)
    {
        // Coven.Core.IMagikBlock doesn't pass CancellationToken; use None.
        var ct = CancellationToken.None;
        var guide = await _guideFactory.CreateAsync(input, ct).ConfigureAwait(false);
        var spell = await _spellFactory.CreateAsync(input, ct).ConfigureAwait(false);
        var test  = await _testFactory .CreateAsync(input, ct).ConfigureAwait(false);

        return await InvokeAsync(input, guide, spell, test, ct).ConfigureAwait(false);
    }

    protected abstract Task<TOut> InvokeAsync(
        TIn input,
        Guidebook<TGuide> guidebook,
        Spellbook<TSpell> spellbook,
        Testbook<TTest>   testbook,
        CancellationToken ct);
}

public abstract class MagikUser<TIn, TOut>
  : MagikUser<TIn, TOut, DefaultGuide, DefaultSpell, DefaultTest>
{
    protected MagikUser()
      : base(new DefaultGuideFactory<TIn>(), new DefaultSpellFactory<TIn>(), new DefaultTestFactory<TIn>())
    { }

    protected MagikUser(
        IGuidebookFactory<TIn, DefaultGuide> guideFactory,
        ISpellbookFactory<TIn, DefaultSpell>  spellFactory,
        ITestbookFactory<TIn, DefaultTest>    testFactory)
      : base(guideFactory, spellFactory, testFactory)
    { }

    // Delegate-based override for defaults (no factory types exposed to user code)
    protected MagikUser(
        Func<TIn, CancellationToken, DefaultGuide>? makeGuide,
        Func<TIn, CancellationToken, DefaultSpell>? makeSpell,
        Func<TIn, CancellationToken, DefaultTest>?  makeTest)
      : base(
            makeGuide is null ? new DefaultGuideFactory<TIn>() : new DelegateGuideFactory<TIn>(makeGuide),
            makeSpell is null ? new DefaultSpellFactory<TIn>()  : new DelegateSpellFactory<TIn>(makeSpell),
            makeTest  is null ? new DefaultTestFactory<TIn>()   : new DelegateTestFactory<TIn>(makeTest))
    { }
}

internal sealed class DelegateGuideFactory<TIn> : IGuidebookFactory<TIn, DefaultGuide>
{
    private readonly Func<TIn, CancellationToken, DefaultGuide> make;
    public DelegateGuideFactory(Func<TIn, CancellationToken, DefaultGuide> make) => this.make = make;
    public Task<Guidebook<DefaultGuide>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Guidebook<DefaultGuide>(make(input, ct)));
}

internal sealed class DelegateSpellFactory<TIn> : ISpellbookFactory<TIn, DefaultSpell>
{
    private readonly Func<TIn, CancellationToken, DefaultSpell> make;
    public DelegateSpellFactory(Func<TIn, CancellationToken, DefaultSpell> make) => this.make = make;
    public Task<Spellbook<DefaultSpell>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Spellbook<DefaultSpell>(make(input, ct)));
}

internal sealed class DelegateTestFactory<TIn> : ITestbookFactory<TIn, DefaultTest>
{
    private readonly Func<TIn, CancellationToken, DefaultTest> make;
    public DelegateTestFactory(Func<TIn, CancellationToken, DefaultTest> make) => this.make = make;
    public Task<Testbook<DefaultTest>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Testbook<DefaultTest>(make(input, ct)));
}
