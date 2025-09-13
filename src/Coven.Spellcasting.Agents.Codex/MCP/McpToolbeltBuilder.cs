// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Linq;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.MCP;

internal static class McpToolbeltBuilder
{
    public static McpToolbelt FromSpells(IReadOnlyList<ISpellContract> spells)
    {
        var tools = new List<McpTool>();
        if (spells is not null)
        {
            foreach (var s in spells)
            {
                var d = s.GetDefinition();
                tools.Add(new McpTool(d.Name, d.InputSchema, d.OutputSchema));
            }
        }
        return new McpToolbelt(tools);
    }
}
