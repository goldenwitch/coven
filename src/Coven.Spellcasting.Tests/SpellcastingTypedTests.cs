using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Spellcasting;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed record ChangeRequest2(string Goal, string RepoRoot);
public sealed record PatchPlan2(string Role, string SpellVersion, IReadOnlyList<string> Cases);

public sealed record MyGuide(string Markdown, string Role);
public sealed record MySpellV1(string Version, IReadOnlyList<object> Steps);
public sealed record MyTestsV1(string Version, IReadOnlyList<string> Cases);

public sealed class MyGuideFactory : IGuidebookFactory<ChangeRequest2, MyGuide>
{
    public Task<IBook<MyGuide>> CreateAsync(ChangeRequest2 input, CancellationToken ct)
        => Task.FromResult<IBook<MyGuide>>(new Guidebook<MyGuide>(new MyGuide("# Guidebook\nTyped", "Senior Assistant")));
}

public sealed class MySpellFactory : ISpellbookFactory<ChangeRequest2, MySpellV1>
{
    public Task<IBook<MySpellV1>> CreateAsync(ChangeRequest2 input, CancellationToken ct)
        => Task.FromResult<IBook<MySpellV1>>(new Spellbook<MySpellV1>(new MySpellV1("0.1", Array.Empty<object>())));
}

public sealed class MyTestFactory : ITestbookFactory<ChangeRequest2, MyTestsV1>
{
    public Task<IBook<MyTestsV1>> CreateAsync(ChangeRequest2 input, CancellationToken ct)
        => Task.FromResult<IBook<MyTestsV1>>(new Testbook<MyTestsV1>(new MyTestsV1("0.1", new[] { "rename_method_happy" })));
}

public sealed class TypedUser : MagikUser<ChangeRequest2, PatchPlan2, MyGuide, MySpellV1, MyTestsV1>
{
    public TypedUser()
        : base(new MyGuideFactory(), new MySpellFactory(), new MyTestFactory()) { }

    protected override Task<PatchPlan2> InvokeAsync(
        ChangeRequest2 input,
        IBook<MyGuide> guide,
        IBook<MySpellV1> spell,
        IBook<MyTestsV1> test,
        CancellationToken ct)
    {
        var plan = new PatchPlan2(guide.Payload.Role, spell.Payload.Version, test.Payload.Cases);
        return Task.FromResult(plan);
    }
}

public class SpellcastingTypedTests
{
    [Fact]
    public async Task MagikUser_TypedBooks_AreSupplied_ViaFactories()
    {
        var user = new TypedUser();
        var coven = new MagikBuilder<ChangeRequest2, PatchPlan2>()
            .MagikBlock<ChangeRequest2, PatchPlan2>(user)
            .Done();

        var input = new ChangeRequest2("demo", "/repo");
        var result = await coven.Ritual<ChangeRequest2, PatchPlan2>(input);

        Assert.NotNull(result);
        Assert.Equal("Senior Assistant", result.Role);
        Assert.Equal("0.1", result.SpellVersion);
        Assert.Collection(result.Cases, c => Assert.Equal("rename_method_happy", c));
    }
}
