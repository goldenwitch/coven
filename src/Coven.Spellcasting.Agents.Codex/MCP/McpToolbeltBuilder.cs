// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Linq;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.MCP;

internal static class McpToolbeltBuilder
{
    public static McpToolbelt FromSpells(IReadOnlyList<SpellDefinition> spells)
    {
        var tools = new List<McpTool>();
        if (spells is not null)
        {
            tools.AddRange(spells.Select(s => new McpTool(s.Name, s.InputSchema, s.OutputSchema)));
        }
        return new McpToolbelt(tools);
    }
}
