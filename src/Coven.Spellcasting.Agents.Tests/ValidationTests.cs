using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Validation;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public class ValidationTests : IDisposable
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

        protected override string GetStampDirectory() => _dir;
    }

    private string NewTempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "coven-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(d);
        _dirs.Add(d);
        return d;
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
    }

    [Fact]
    public async Task Skips_When_No_RunCommand_Permission()
    {
        var dir = NewTempDir();
        var v = new TestValidator("test-agent", "v1", dir);
        var ctx = new SpellContext { Permissions = AgentPermissions.None() };

        var res = await v.ValidateAsync(ctx);
        Assert.Equal(AgentValidationOutcome.Skipped, res.Outcome);
        Assert.Equal(0, v.ProvisionCalls);
    }

    [Fact]
    public async Task Idempotent_With_Stamp()
    {
        var dir = NewTempDir();
        var v = new TestValidator("test-agent", "v1", dir);
        var ctx = new SpellContext { Permissions = AgentPermissions.FullAuto() };

        var first = await v.ValidateAsync(ctx);
        var second = await v.ValidateAsync(ctx);

        Assert.Equal(AgentValidationOutcome.Performed, first.Outcome);
        Assert.Equal(AgentValidationOutcome.Noop, second.Outcome);
        Assert.Equal(1, v.ProvisionCalls);
    }

    [Fact]
    public async Task Revalidates_When_Spec_Changes()
    {
        var dir = NewTempDir();
        var v1 = new TestValidator("test-agent", "v1", dir);
        var v2 = new TestValidator("test-agent", "v2", dir);
        var ctx = new SpellContext { Permissions = AgentPermissions.FullAuto() };

        var first = await v1.ValidateAsync(ctx);
        var second = await v2.ValidateAsync(ctx);

        Assert.Equal(AgentValidationOutcome.Performed, first.Outcome);
        Assert.Equal(AgentValidationOutcome.Performed, second.Outcome);
        Assert.Equal(1, v1.ProvisionCalls);
        Assert.Equal(1, v2.ProvisionCalls);
    }
}
