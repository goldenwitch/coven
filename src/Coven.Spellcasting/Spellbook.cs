// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

/// <summary>
/// Immutable collection of spell definitions and their corresponding instances.
/// Produced by <see cref="SpellbookBuilder"/> for discovery and wiring.
/// </summary>
public sealed record Spellbook
{
    /// <summary>
    /// All discovered spell definitions (names and schemas).
    /// </summary>
    public IReadOnlyList<SpellDefinition> Definitions { get; init; }

    /// <summary>
    /// The concrete spell instances corresponding to <see cref="Definitions"/>.
    /// </summary>
    public IReadOnlyList<object> Spells { get; init; }

    /// <summary>
    /// Create a new spellbook from definitions and spell instances.
    /// </summary>
    /// <param name="definitions">Canonical definitions for each spell.</param>
    /// <param name="spells">Concrete spell instances.</param>
    public Spellbook(IReadOnlyList<SpellDefinition> definitions, IReadOnlyList<object> spells)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Spells = spells ?? throw new ArgumentNullException(nameof(spells));
    }
}
