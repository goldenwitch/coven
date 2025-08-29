using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Validation;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class ValidationIntegrationTests : IDisposable
{
    private readonly System.Collections.Generic.List<string> _dirs = new();
    private sealed class TestValidator : IdempotentAgentValidation
    {
        private readonly string _spec;
        private readonly string _dir;
        public int ProvisionCalls { get; private set; }

        public TestValidator(string agentId, string spec, string dir) : base(agentId)
        { _spec = spec; _dir = dir; }

        protected override string ComputeSpec(SpellContext? context) => _spec;

        protected override Task ProvisionAsync(SpellContext? context, CancellationToken ct)
        { ProvisionCalls++; return Task.CompletedTask; }

        protected override string GetStampDirectory(SpellContext? _) => _dir;
    }

    private sealed class FakeAgent : ICovenAgent<FixSpell, string>
    {
        public string Id => "fake";
        public Task<string> CastSpellAsync(FixSpell input, SpellContext? context = null, CancellationToken ct = default)
        {
            var mode = context?.Permissions?.Allows<WriteFile>() == true ? "edit" : "suggest";
            return Task.FromResult($"{input.Goal}|{mode}");
        }
    }

    public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

    private sealed class SpellUserFromContext : MagikUser<SpellContext, string>
    {
        private readonly ICovenAgent<FixSpell, string> _agent;
        public SpellUserFromContext(ICovenAgent<FixSpell, string> agent) => _agent = agent;

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
                "task");
            return _agent.CastSpellAsync(payload, input, ct);
        }
    }

    private string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "coven-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(d);
        _dirs.Add(d);
        return d;
    }

    [Fact]
    public async Task Validate_Block_Composes_In_Pipeline()
    {
        var dir = NewTempDir();
        var validator = new TestValidator("test-agent", "v1", dir);
        var agent = new FakeAgent();

        var coven = new MagikBuilder<SpellContext, string>()
            .MagikBlock(new ValidateAgentBlock(validator))
            .MagikBlock<SpellContext, string>(new SpellUserFromContext(agent))
            .Done();

        var ctx = new SpellContext
        {
            ContextUri = new Uri("file:///tmp/repo"),
            Permissions = AgentPermissions.FullAuto()
        };

        var out1 = await coven.Ritual<SpellContext, string>(ctx);
        var out2 = await coven.Ritual<SpellContext, string>(ctx);

        Assert.Contains("task", out1);
        Assert.Contains("edit", out1);
        Assert.Equal(1, validator.ProvisionCalls); // idempotent: second run is noop
        Assert.Equal(out1, out2); // deterministic in this fake setup
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
    }
}
