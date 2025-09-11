// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.MCP;

public sealed class McpToolbelt
{
    public IReadOnlyList<McpTool> Tools { get; }

    public McpToolbelt(IReadOnlyList<McpTool> tools)
    {
        Tools = tools;
    }

    public string ToJson()
    {
        var payload = new
        {
            tools = Tools
        };
        return JsonSerializer.Serialize(payload);
    }
}