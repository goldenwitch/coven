// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting.Grimoire;
using Coven.Spellcasting.Agents;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

public sealed class AmbientAgentIntegrationTests
{
    private sealed class FakeAgent : ICovenAgent<string>
    {
        public bool Closed { get; private set; }

        public Task RegisterSpells(List<Spells.SpellDefinition> spells) => Task.CompletedTask;
        public Task InvokeAgent(CancellationToken ct = default) => Task.CompletedTask;
        public Task CloseAgent() { Closed = true; return Task.CompletedTask; }

    }

    private sealed class CancelBlock : IMagikBlock<Empty, Empty>
    {
        public async Task<Empty> DoMagik(Empty input)
        {
            var spell = new CancelAgent();
            await spell.CastSpell();
            return Empty.Value;
        }
    }

    [Fact]
    public async Task AddCovenAgent_registers_control_and_cancel_spell_closes_agent()
    {
        var services = new ServiceCollection();
        services.AddCovenAgent<string, FakeAgent>();
        services.BuildCoven(c => { c.AddBlock<Empty, Empty, CancelBlock>(); c.Done(); });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        _ = await coven.Ritual<Empty>();

        var agent = (FakeAgent)sp.GetRequiredService<ICovenAgent<string>>();
        Assert.True(agent.Closed);
        var control = sp.GetRequiredService<IAgentControl>();
        Assert.Same(agent, control);
    }
}