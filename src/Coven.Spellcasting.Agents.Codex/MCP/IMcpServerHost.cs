// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting.Agents.Codex.MCP;

public interface IMcpServerSession : IAsyncDisposable
{
    string ToolbeltPath { get; }
    string? PipeName { get; }
}

public interface IMcpServerHost
{
    Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default);
    Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, Exec.IMcpSpellExecutorRegistry registry, CancellationToken ct = default);
}