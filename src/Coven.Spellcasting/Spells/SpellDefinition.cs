// SPDX-License-Identifier: BUSL-1.1

using System;

namespace Coven.Spellcasting.Spells;

/// <summary>
/// Represents the necessary details for a spell to be cast.
/// </summary>
public record SpellDefinition(string Name, string? InputSchema = null, string? OutputSchema = null);