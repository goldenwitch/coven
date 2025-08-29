using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Di;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed record ChangeIn(string Goal);
public sealed record ChangeOut(string GuideMd, string SpellVer, string Suite);

// Custom overrides for default book types
public sealed class CustomGuideFactory : IGuidebookFactory<ChangeIn, DefaultGuide>
{
    public int Calls;
    public Task<Guidebook<DefaultGuide>> CreateAsync(ChangeIn input, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(new Guidebook<DefaultGuide>(new DefaultGuide("# DI Guide")));
    }
}

public sealed class CustomSpellFactory : ISpellbookFactory<ChangeIn, DefaultSpell>
{
    public int Calls;
    public Task<Spellbook<DefaultSpell>> CreateAsync(ChangeIn input, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(new Spellbook<DefaultSpell>(new DefaultSpell("9.9")));
    }
}

public sealed class CustomTestFactory : ITestbookFactory<ChangeIn, DefaultTest>
{
    public int Calls;
    public Task<Testbook<DefaultTest>> CreateAsync(ChangeIn input, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(new Testbook<DefaultTest>(new DefaultTest("regression")));
    }
}

// User inheriting the defaulted base, but taking DI-provided default-type factories
public sealed class DefaultedUserWithOverrides : MagikUser<ChangeIn, ChangeOut>
{
    public DefaultedUserWithOverrides(
        IGuidebookFactory<ChangeIn, DefaultGuide> guide,
        ISpellbookFactory<ChangeIn, DefaultSpell> spell,
        ITestbookFactory<ChangeIn, DefaultTest> test)
        : base(guide, spell, test) { }

    protected override Task<ChangeOut> InvokeAsync(
        ChangeIn input,
        Guidebook<DefaultGuide> guide,
        Spellbook<DefaultSpell> spell,
        Testbook<DefaultTest>   test,
        CancellationToken ct)
    {
        return Task.FromResult(new ChangeOut(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite));
    }
}

public class SpellcastingDiDefaultOverrideTests
{
    [Fact]
    public async Task Defaulted_MagikUser_Uses_DI_Provided_Default_Factories()
    {
        var services = new ServiceCollection();
        var g = new CustomGuideFactory();
        var s = new CustomSpellFactory();
        var t = new CustomTestFactory();
        services.AddSingleton<IGuidebookFactory<ChangeIn, DefaultGuide>>(g);
        services.AddSingleton<ISpellbookFactory<ChangeIn, DefaultSpell>>(s);
        services.AddSingleton<ITestbookFactory<ChangeIn, DefaultTest>>(t);

        services.BuildCoven(c =>
        {
            c.AddBlock<ChangeIn, ChangeOut, DefaultedUserWithOverrides>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<ChangeIn, ChangeOut>(new ChangeIn("demo"));

        Assert.Equal("# DI Guide", result.GuideMd);
        Assert.Equal("9.9", result.SpellVer);
        Assert.Equal("regression", result.Suite);
        Assert.Equal(1, g.Calls);
        Assert.Equal(1, s.Calls);
        Assert.Equal(1, t.Calls);
    }
}

// Typed DI scenario
public sealed record TIn(string Topic);
public sealed record TOut(string Role, string Ver, IReadOnlyList<string> Cases);
public sealed record MyGuide2(string Markdown, string Role);
public sealed record MySpell2(string Version);
public sealed record MyTests2(IReadOnlyList<string> Cases);

public sealed class MyGuide2Factory : IGuidebookFactory<TIn, MyGuide2>
{
    public Task<Guidebook<MyGuide2>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Guidebook<MyGuide2>(new MyGuide2("# Typed", "Architect")));
}

public sealed class MySpell2Factory : ISpellbookFactory<TIn, MySpell2>
{
    public Task<Spellbook<MySpell2>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Spellbook<MySpell2>(new MySpell2("1.2.3")));
}

public sealed class MyTests2Factory : ITestbookFactory<TIn, MyTests2>
{
    public Task<Testbook<MyTests2>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Testbook<MyTests2>(new MyTests2(new[] { "caseA" })));
}

public sealed class TypedUserDi : MagikUser<TIn, TOut, MyGuide2, MySpell2, MyTests2>
{
    public TypedUserDi(
        IGuidebookFactory<TIn, MyGuide2> guide,
        ISpellbookFactory<TIn, MySpell2> spell,
        ITestbookFactory<TIn, MyTests2>  test)
        : base(guide, spell, test) { }

    protected override Task<TOut> InvokeAsync(
        TIn input,
        Guidebook<MyGuide2> guide,
        Spellbook<MySpell2> spell,
        Testbook<MyTests2>  test,
        CancellationToken ct)
    {
        return Task.FromResult(new TOut(guide.Payload.Role, spell.Payload.Version, test.Payload.Cases));
    }
}

public class SpellcastingDiTypedTests
{
    [Fact]
    public async Task Typed_MagikUser_Is_Composed_From_DI_Factories()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGuidebookFactory<TIn, MyGuide2>, MyGuide2Factory>();
        services.AddSingleton<ISpellbookFactory<TIn, MySpell2>, MySpell2Factory>();
        services.AddSingleton<ITestbookFactory<TIn, MyTests2>, MyTests2Factory>();

        services.BuildCoven(c =>
        {
            c.AddBlock<TIn, TOut, TypedUserDi>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<TIn, TOut>(new TIn("hello"));

        Assert.Equal("Architect", result.Role);
        Assert.Equal("1.2.3", result.Ver);
        Assert.Collection(result.Cases, c => Assert.Equal("caseA", c));
    }
}

