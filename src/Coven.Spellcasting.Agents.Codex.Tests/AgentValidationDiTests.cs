using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;
using Coven.Chat;
using Coven.Spellcasting.Agents.Validation;
using Coven.Spellcasting.Agents.Codex.Di;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class AgentValidationDiTests
{
    private sealed class FakeValidation : IAgentValidation
    {
        public string AgentId => "Codex";
        public int Calls;
        public Task<AgentValidationResult> ValidateAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(AgentValidationResult.Noop("fake"));
        }
    }

    [Fact]
    public async Task ValidateAgentBlock_Is_Invoked_Via_DI_Pipeline()
    {
        var services = new ServiceCollection();

        // Minimal dependencies: scrivener for Codex DI wiring
        services.AddSingleton<IScrivener<string>, InMemoryScrivener<string>>();

        // Wire Codex agent DI (options don't matter for this test)
        services.AddCodexCliAgent(o =>
        {
            o.ExecutablePath = "codex";
            o.WorkspaceDirectory = Path.GetTempPath();
            o.ShimExecutablePath = null;
        });

        // Override validator with a test double to avoid environment dependencies
        var fake = new FakeValidation();
        services.AddSingleton<IAgentValidation>(fake);

        // Build a tiny Coven pipeline that runs the validator block
        services.BuildCoven(c =>
        {
            c.AddBlock<Empty, Empty, ValidateAgentBlock>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        _ = await coven.Ritual<Empty>();

        Assert.Equal(1, fake.Calls);
    }
}

