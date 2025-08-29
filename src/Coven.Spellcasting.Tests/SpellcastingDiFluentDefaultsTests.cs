using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;
using Xunit;

// NOTE: This test targets the planned DI helpers under Coven.Spellcasting.Di
// It will fail to build until the extension and builder are implemented.

namespace Coven.Spellcasting.Tests;

using Coven.Core;
using Coven.Spellcasting;
using Coven.Spellcasting.Di;

public sealed record Req(string Goal);
public sealed record Resp(string Guide, string Spell, string Suite, string Role, string Kind, string Track);

// Derived payloads from defaults
public sealed record MyGuide3(string Markdown, string Role) : DefaultGuide(Markdown);
public sealed record MySpell3(string Version, string Kind) : DefaultSpell(Version);
public sealed record MyTest3(string Suite, string Track) : DefaultTest(Suite);

// User stays on the defaulted base but accepts base-typed factories for DI
public sealed class UserDefaultedViaDi : MagikUser<Req, Resp>
{
    public UserDefaultedViaDi(
        IGuidebookFactory<Req, DefaultGuide> guide,
        ISpellbookFactory<Req, DefaultSpell> spell,
        ITestbookFactory<Req, DefaultTest>   test)
        : base(guide, spell, test) { }

    protected override Task<Resp> InvokeAsync(
        Req input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        // Downcast is safe if DI provided derived payloads; otherwise properties may be null/default
        var g = guide.Payload as MyGuide3;
        var s = spell.Payload as MySpell3;
        var t = test.Payload  as MyTest3;

        return Task.FromResult(new Resp(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite,
            g?.Role ?? string.Empty,
            s?.Kind ?? string.Empty,
            t?.Track ?? string.Empty));
    }
}

public class SpellcastingDiFluentDefaultsTests
{
    [Fact]
    public async Task AddSpellcastingDefaults_Overrides_Default_Books_Via_DI()
    {
        var services = new ServiceCollection();

        services.AddSpellcastingDefaults<Req>(b =>
            b.UseGuide((req, ct) => new MyGuide3("# guide fluent", "Architect"))
             .UseSpell((req, ct) => new MySpell3("7.7", "std"))
             .UseTest((req, ct)  => new MyTest3("regression", "A"))
        );

        services.BuildCoven(c =>
        {
            c.AddBlock<Req, Resp, UserDefaultedViaDi>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<Req, Resp>(new Req("demo"));

        Assert.Equal("# guide fluent", result.Guide);
        Assert.Equal("7.7", result.Spell);
        Assert.Equal("regression", result.Suite);
        Assert.Equal("Architect", result.Role);
        Assert.Equal("std", result.Kind);
        Assert.Equal("A", result.Track);
    }
}
