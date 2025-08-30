using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public sealed record ChangeRequest(string RepoRoot, string Goal);
public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

// Single MagikUser that prepares books and calls the agent
public sealed class EndToEndUser : MagikUser<ChangeRequest, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    public EndToEndUser(ICovenAgent<FixSpell, string> agent)
    { _agent = agent; }

    protected override Task<string> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        var payload = new FixSpell(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite,
            input.Goal);
        // Build SpellContext locally based on input
        var ctx = new SpellContext
        {
            ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
            Permissions = AgentPermissions.AutoEdit()
        };
        return _agent.CastSpellAsync(payload, ctx, ct);
    }
}

// Fake agent that inspects SpellContext and returns a summary string
public sealed class FakeAgent : ICovenAgent<FixSpell, string>
{
    public string Id => "fake";

    public Task<string> CastSpellAsync(
        FixSpell input,
        SpellContext? context = null,
        CancellationToken ct = default)
    {
        var canEdit = context?.Permissions?.Allows<WriteFile>() == true ? "edit" : "suggest";
        var cwd = context?.ContextUri?.IsAbsoluteUri == true ? context.ContextUri.LocalPath : string.Empty;
        var result = $"{input.Goal}|{input.SpellVersion}|{input.TestSuite}|{canEdit}|{cwd}";
        return Task.FromResult(result);
    }
}

public class E2EIntegrationTests
{
    [Fact]
    public async Task Pipeline_Uses_Spellcasting_And_Agent_With_Context()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);

        var services = new ServiceCollection();
        services.AddSingleton<ICovenAgent<FixSpell, string>, FakeAgent>();
        services.BuildCoven(c =>
        {
            c.AddBlock<ChangeRequest, string, EndToEndUser>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "demo-task"));

        Assert.Contains("demo-task", result);
        Assert.Contains("0.1", result);       // default spell version
        Assert.Contains("smoke", result);     // default test suite
        Assert.Contains("edit", result);      // permission reflected
        Assert.Contains(Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar), result);
    }
}
