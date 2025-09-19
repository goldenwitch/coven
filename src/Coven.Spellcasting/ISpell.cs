// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// Produces TOut during tool execution.
/// </summary>
public interface ISpell<TIn, TOut> : ISpell
{
    public new SpellDefinition Definition
    {
        get => new SpellDefinition(
            SchemaGen.GetFriendlyName(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TOut)));
    }
    public Task<TOut> CastSpell(TIn Input);
}

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// </summary>
public interface ISpell<TIn> : ISpell
{
    public new SpellDefinition Definition
    {
        get => new SpellDefinition(
            SchemaGen.GetFriendlyName(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TIn)));
    }
    public Task CastSpell(TIn Input);
}


/// <summary>
/// Represents a tool that is usable by any agent.
/// No input or output
/// </summary>
public interface ISpell
{
    public SpellDefinition Definition
    {
        get => new SpellDefinition(SchemaGen.GetFriendlyName(GetType()));
    }
    public Task CastSpell();
}
