// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.MCP;

public sealed record McpTool(
    string Name,
    string? InputSchema,
    string? OutputSchema);