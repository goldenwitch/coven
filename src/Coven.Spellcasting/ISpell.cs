// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// Produces TOut during tool execution.
/// </summary>
public interface ISpell<TIn, TOut> : ISpell
{
    new SpellDefinition Definition
    {
        get => new(
            SchemaGen.GetFriendlyName(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TOut)));
    }
    Task<TOut> CastSpell(TIn Input);
}

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// </summary>
public interface ISpell<TIn> : ISpell
{
    new SpellDefinition Definition
    {
        get => new(
            SchemaGen.GetFriendlyName(typeof(TIn)),
            SchemaGen.GenerateSchema(typeof(TIn)));
    }
    Task CastSpell(TIn Input);
}


/// <summary>
/// Represents a tool that is usable by any agent.
/// No input or output
/// </summary>
public interface ISpell
{
    SpellDefinition Definition
    {
        get => new(SchemaGen.GetFriendlyName(GetType()));
    }
    Task CastSpell();
}
