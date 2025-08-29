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

public class E2EValidationPipelineTests
{
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
        protected override string GetStampDirectory() => _dir;
    }

    private sealed class FakeAgent : ICovenAgent<FixSpell, string>
    {
        public string Id => "fake";
        public Task<string> CastSpellAsync(FixSpell input, SpellContext? context = null, CancellationToken ct = default)
        {
            var cwd = context?.ContextUri?.IsAbsoluteUri == true ? context.ContextUri.LocalPath : string.Empty;
            return Task.FromResult($"{input.Goal}|{input.SpellVersion}|{input.TestSuite}|{cwd}");
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

    private static string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "coven-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public async Task EndToEnd_With_Validation_Block()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);

        var dir = NewTempDir();
        var validator = new TestValidator("test-agent", "v1", dir);
        var agent = new FakeAgent();

        var coven = new MagikBuilder<ChangeRequest, string>()
            .MagikBlock(new MakeContextBlock())
            .MagikBlock(new ValidateAgentBlock(validator))
            .MagikBlock<SpellContext, string>(new SpellUserFromContext(agent, "goal"))
            .Done();

        var result = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "goal"));

        Assert.Contains("goal", result);
        Assert.Contains("0.1", result); // default spell version
        Assert.Contains("smoke", result); // default test suite
        Assert.Contains(Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar), result);
        Assert.Equal(1, validator.ProvisionCalls);
    }
}

