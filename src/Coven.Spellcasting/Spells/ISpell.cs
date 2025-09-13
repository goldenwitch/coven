// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Spells;

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// Produces TOut during tool execution.
/// </summary>
public interface ISpell<TIn, TOut> : ISpellContract
{
    public SpellDefinition GetDefinition()
    {
        return new SpellDefinition(SchemaGen.GetFriendlyName(typeof(TIn)), SchemaGen.GenerateSchema(typeof(TIn)), SchemaGen.GenerateSchema(typeof(TOut)));
    }
    public Task<TOut> CastSpell(TIn Input);
}

/// <summary>
/// Represents a tool that is usable by any agent.
/// Builds a definition from the input shape TIn
/// </summary>
public interface ISpell<TIn>
    : ISpellContract
{
    public SpellDefinition GetDefinition()
    {
        return new SpellDefinition(SchemaGen.GetFriendlyName(typeof(TIn)), SchemaGen.GenerateSchema(typeof(TIn)));
    }
    public Task CastSpell(TIn Input);
}


/// <summary>
/// Represents a tool that is usable by any agent.
/// No input or output
/// </summary>
public interface ISpell
    : ISpellContract
{
    public SpellDefinition GetDefinition()
    {
        return new SpellDefinition(SchemaGen.GetFriendlyName(GetType()));
    }

    public Task CastSpell();
}
