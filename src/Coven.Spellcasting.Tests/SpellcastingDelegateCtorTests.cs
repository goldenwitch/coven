using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed record CIn(string Goal);
public sealed record COut(string Guide, string Spell, string Suite);

public sealed class UserWithCtorDelegates : MagikUser<CIn, COut>
{
    public UserWithCtorDelegates() : base(
        (input, ct) => new DefaultGuide("# ctor guide"),
        (input, ct) => new DefaultSpell("2.0"),
        (input, ct) => new DefaultTest("nightly"))
    { }

    protected override Task<COut> InvokeAsync(
        CIn input,
        Guidebook<DefaultGuide> guide,
        Spellbook<DefaultSpell> spell,
        Testbook<DefaultTest>   test,
        CancellationToken ct)
    {
        return Task.FromResult(new COut(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite));
    }
}

public class SpellcastingDelegateCtorTests
{
    [Fact]
    public async Task DelegateCtor_Overrides_DefaultBooks()
    {
        var user = new UserWithCtorDelegates();
        var coven = new MagikBuilder<CIn, COut>()
            .MagikBlock<CIn, COut>(user)
            .Done();

        var result = await coven.Ritual<CIn, COut>(new CIn("demo"));
        Assert.Equal("# ctor guide", result.Guide);
        Assert.Equal("2.0", result.Spell);
        Assert.Equal("nightly", result.Suite);
    }
}

