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
}
