// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// A spell with typed input and output.
/// Builds its <see cref="SpellDefinition"/> from <typeparamref name="TIn"/> and <typeparamref name="TOut"/>.
/// </summary>
/// <typeparam name="TIn">Input shape consumed by the spell.</typeparam>
/// <typeparam name="TOut">Output shape produced by the spell.</typeparam>
public interface ISpell<TIn, TOut> : ISpell
{
    /// <summary>
    /// Canonical definition (name and schemas) for this spell.
    /// </summary>
    new SpellDefinition Definition => new(
        SchemaGen.GetFriendlyName(typeof(TIn)),
        SchemaGen.GenerateSchema(typeof(TIn)),
        SchemaGen.GenerateSchema(typeof(TOut)));

    /// <summary>
    /// Execute the spell with the provided input.
    /// </summary>
    /// <param name="Input">The input payload to process.</param>
    /// <returns>The produced <typeparamref name="TOut"/> value.</returns>
    Task<TOut> CastSpell(TIn Input);
}

/// <summary>
/// A spell with typed input and no output.
/// Builds its <see cref="SpellDefinition"/> from <typeparamref name="TIn"/>.
/// </summary>
/// <typeparam name="TIn">Input shape consumed by the spell.</typeparam>
public interface ISpell<TIn> : ISpell
{
    /// <summary>
    /// Canonical definition (name and schemas) for this spell.
    /// </summary>
    new SpellDefinition Definition => new(
        SchemaGen.GetFriendlyName(typeof(TIn)),
        SchemaGen.GenerateSchema(typeof(TIn)));

    /// <summary>
    /// Execute the spell with the provided input.
    /// </summary>
    /// <param name="Input">The input payload to process.</param>
    Task CastSpell(TIn Input);
}


/// <summary>
/// Base spell contract usable by any agent.
/// No input or output.
/// </summary>
public interface ISpell
{
    /// <summary>
    /// Canonical definition (name and schemas) for this spell.
    /// Default name derives from the implementing type.
    /// </summary>
    SpellDefinition Definition => new(SchemaGen.GetFriendlyName(GetType()));

    /// <summary>
    /// Execute the spell.
    /// </summary>
    Task CastSpell();
}
