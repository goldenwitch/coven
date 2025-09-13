// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Spells;

/// <summary>
/// Non-generic spell contract that exposes the canonical spell definition.
/// All spells implement this to provide name and schemas via the Spellbook.
/// </summary>
public interface ISpellContract
{
    /// <summary>
    /// Returns the canonical definition for this spell.
    /// The Spellbook is the source of truth for the returned name and schemas.
    /// </summary>
    SpellDefinition GetDefinition();
}

