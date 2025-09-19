// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// Non-generic spell contract that exposes the canonical spell definition.
/// All spells implement this to provide name and schemas via the Spellbook.
/// </summary>
public interface ISpellContract
{
    /// <summary>
    /// Canonical spell definition (name and schemas). Prefer values supplied by the Spellbook.
    /// Default provides a friendly name; n-ary spell interfaces supply schema-aware defaults.
    /// </summary>
    public SpellDefinition Definition
    {
        get => new SpellDefinition(SchemaGen.GetFriendlyName(GetType()));
    }
}
