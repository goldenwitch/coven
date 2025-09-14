// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents.Codex.MCP.Tools;

namespace Coven.Spellcasting.Agents.Codex.MCP;

public interface IMcpServerSession : IAsyncDisposable
{
    string ToolbeltPath { get; }
    string? PipeName { get; }
}

public interface IMcpServerHost
{
    Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, IMcpSpellExecutorRegistry? registry = null, CancellationToken ct = default);
}
