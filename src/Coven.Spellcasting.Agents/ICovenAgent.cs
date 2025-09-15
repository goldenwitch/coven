// SPDX-License-Identifier: BUSL-1.1

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
    public Task RegisterSpells(IReadOnlyList<ISpellContract> spells, CancellationToken ct = default);

    public Task InvokeAgent(CancellationToken ct = default);
}
