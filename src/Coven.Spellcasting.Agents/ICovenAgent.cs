using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents;

public interface ICovenAgent<TIn, TMessageFormat, TOut> : ICovenAgent<TMessageFormat>
{
    public string Id { get; }

    public Task InvokeAgent(TIn input, CancellationToken ct = default);
    public Task<TOut> CloseAgentWithResult();
}

public interface ICovenAgent<TMessageFormat> : IAgentControl
{
    Task<TMessageFormat> ReadMessage();
    Task SendMessage(TMessageFormat message);

    public Task RegisterSpells(List<SpellDefinition> spells);

    public Task InvokeAgent(CancellationToken ct = default);
}
