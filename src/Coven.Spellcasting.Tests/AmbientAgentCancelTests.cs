// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting.Grimoire;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed class AmbientAgentCancelTests
{
    private sealed class TestControl
    {
        public bool Closed { get; private set; }
        public Task CloseAsync() { Closed = true; return Task.CompletedTask; }
    }

    private sealed class TestEnv : AmbientAgent.IAgentEnvironment
    {
        public async Task CancelAsync(IServiceProvider? sp)
        {
            if (sp is null) return;
            var ctrl = sp.GetRequiredService<TestControl>();
            await ctrl.CloseAsync();
        }
    }

    private sealed class CancelBlock : IMagikBlock<Empty, Empty>
    {
        public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
        {
            var spell = new CancelAgent();
            await spell.CastSpell();
            return Empty.Value;
        }
    }

    [Fact]
    public async Task CancelAgent_spell_invokes_environment_cancel_within_ritual_scope()
    {
        // Arrange: configure AmbientAgent to use TestEnv and DI to provide TestControl
        AmbientAgent.Configure(new TestEnv());
        var services = new ServiceCollection();
        services.AddSingleton<TestControl>();
        services.BuildCoven(c => { c.AddBlock<Empty, Empty, CancelBlock>(); c.Done(); });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        // Act: run a ritual that executes the block which casts CancelAgent
        _ = await coven.Ritual<Empty>();

        // Assert: the test control was closed by the environment via AmbientAgent
        var ctrl = sp.GetRequiredService<TestControl>();
        Assert.True(ctrl.Closed);
    }
}
