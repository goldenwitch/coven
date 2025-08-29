using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Validation;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class E2EIntegrationCodexValidationTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _dirs = new();

    private string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "coven-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(d);
        _dirs.Add(d);
        return d;
    }
    public sealed record ChangeRequest(string RepoRoot, string Goal);
    public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

    private sealed class MakeContextBlock : Coven.Core.IMagikBlock<ChangeRequest, SpellContext>
    {
        public Task<SpellContext> DoMagik(ChangeRequest input)
        {
            var ctx = new SpellContext
            {
                ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
                Permissions = AgentPermissions.FullAuto()
            };
            return Task.FromResult(ctx);
        }
    }

    private sealed class FakeAgent : ICovenAgent<FixSpell, string>
    {
        public string Id => "fake";
        public Task<string> CastSpellAsync(FixSpell input, SpellContext? context = null, CancellationToken ct = default)
        {
            var mode = context?.Permissions?.Allows<WriteFile>() == true ? "edit" : "suggest";
            return Task.FromResult($"{input.Goal}|{input.SpellVersion}|{input.TestSuite}|{mode}");
        }
    }

    private sealed class SpellUserFromContext : MagikUser<SpellContext, string>
    {
        private readonly ICovenAgent<FixSpell, string> _agent;
        private readonly string _goal;
        public SpellUserFromContext(ICovenAgent<FixSpell, string> agent, string goal)
        { _agent = agent; _goal = goal; }

        protected override Task<string> InvokeAsync(
            SpellContext input,
            IBook<DefaultGuide> guide,
            IBook<DefaultSpell> spell,
            IBook<DefaultTest>  test,
            CancellationToken ct)
        {
            var payload = new FixSpell(
                guide.Payload.Markdown,
                spell.Payload.Version,
                test.Payload.Suite,
                _goal);
            return _agent.CastSpellAsync(payload, input, ct);
        }
    }

    [Fact]
    public async Task Pipeline_Validates_Codex_Before_Running()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);

        var called = 0;
        var validation = new CodexCliValidation(new CodexCliValidation.Options
        {
            ProbeAsync = (ctx, ct) => Task.FromResult(false),
            InstallerAsync = (ctx, ct) => { called++; return Task.CompletedTask; },
            StampDirectory = NewTempDir()
        });

        var agent = new FakeAgent();

        var coven = new MagikBuilder<ChangeRequest, string>()
            .MagikBlock(new MakeContextBlock())
            .MagikBlock(new ValidateAgentBlock(validation))
            .MagikBlock<SpellContext, string>(new SpellUserFromContext(agent, "goal"))
            .Done();

        var result1 = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "goal"));
        var result2 = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "goal"));

        Assert.Contains("goal", result1);
        Assert.Contains("0.1", result1);
        Assert.Contains("smoke", result1);
        Assert.Contains("edit", result1);
        Assert.Equal(1, called); // installer ran once, stamp made it idempotent
        Assert.Equal(result1, result2);
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
        // Clean up Codex default stamp directory (best-effort)
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(root, "Coven", "agents", "codex");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { /* ignore */ }
    }
}
