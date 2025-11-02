// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// Builder for composing a <see cref="Spellbook"/> from one or more spells.
/// </summary>
public sealed class SpellbookBuilder
{
    private readonly List<SpellDefinition> _definitions = [];
    private readonly List<object> _spells = [];

    /// <summary>
    /// Add a non-generic spell.
    /// </summary>
    /// <param name="spell">The spell instance to include.</param>
    /// <returns>This builder for chaining.</returns>
    public SpellbookBuilder AddSpell(ISpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    /// <summary>
    /// Add a typed-input spell.
    /// </summary>
    /// <typeparam name="TIn">Input shape consumed by the spell.</typeparam>
    /// <param name="spell">The spell instance to include.</param>
    /// <returns>This builder for chaining.</returns>
    public SpellbookBuilder AddSpell<TIn>(ISpell<TIn> spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    /// <summary>
    /// Add a typed-input/output spell.
    /// </summary>
    /// <typeparam name="TIn">Input shape consumed by the spell.</typeparam>
    /// <typeparam name="TOut">Output shape produced by the spell.</typeparam>
    /// <param name="spell">The spell instance to include.</param>
    /// <returns>This builder for chaining.</returns>
    public SpellbookBuilder AddSpell<TIn, TOut>(ISpell<TIn, TOut> spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        _spells.Add(spell);
        _definitions.Add(spell.Definition);
        return this;
    }

    /// <summary>
    /// Build an immutable <see cref="Spellbook"/> containing all added spells and definitions.
    /// </summary>
    /// <returns>The constructed spellbook.</returns>
    public Spellbook Build()
    {
        return new Spellbook(_definitions.AsReadOnly(), _spells.AsReadOnly());
    }
}
