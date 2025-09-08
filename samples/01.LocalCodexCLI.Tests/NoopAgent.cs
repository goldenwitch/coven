using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Spells;

namespace Coven.Samples.LocalCodexCLI.Tests;

internal sealed class NoopAgent : ICovenAgent<string>
{
    private readonly List<SpellDefinition> _defs = new();

    public Task RegisterSpells(List<SpellDefinition> spells)
    {
        _defs.Clear();
        if (spells is not null) _defs.AddRange(spells);
        return Task.CompletedTask;
    }

    public Task InvokeAgent(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CloseAgent()
    {
        return Task.CompletedTask;
    }

    Task<string> ICovenAgent<string>.ReadMessage() => Task.FromResult(string.Empty);
    Task ICovenAgent<string>.SendMessage(string message) => Task.CompletedTask;
}

