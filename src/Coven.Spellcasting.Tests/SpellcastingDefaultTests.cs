using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Spellcasting;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed record ChangeRequest(string Goal, string RepoRoot);
public sealed record PatchPlan(string GuideMarkdown, string SpellVersion, string TestSuite);

public sealed class DefaultUser : MagikUser<ChangeRequest, PatchPlan>
{
    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        Guidebook<DefaultGuide> guide,
        Spellbook<DefaultSpell> spell,
        Testbook<DefaultTest>   test,
        CancellationToken ct)
    {
        var plan = new PatchPlan(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite);
        return Task.FromResult(plan);
    }
}

public class SpellcastingDefaultTests
{
    [Fact]
    public async Task MagikUserStd_Receives_DefaultBooks_And_Completes()
    {
        var user = new DefaultUser();
        var coven = new MagikBuilder<ChangeRequest, PatchPlan>()
            .MagikBlock<ChangeRequest, PatchPlan>(user)
            .Done();

        var input = new ChangeRequest("demo", "/repo");
        var result = await coven.Ritual<ChangeRequest, PatchPlan>(input);

        Assert.NotNull(result);
        Assert.Contains("# Guidebook", result.GuideMarkdown);
        Assert.Equal("0.1", result.SpellVersion);
        Assert.Equal("smoke", result.TestSuite);
    }
}
