// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Spells;

/// <summary>
/// Non-generic spell contract that exposes the canonical spell definition.
/// All spells implement this to provide name and schemas via the Spellbook.
/// </summary>
public interface ISpellContract
{
    /// <summary>
    /// Canonical spell definition (name and schemas). Prefer values supplied by the Spellbook.
    /// Implementations provide defaults via the n-ary spell interfaces.
    /// </summary>
    public SpellDefinition Definition { get; }
}
