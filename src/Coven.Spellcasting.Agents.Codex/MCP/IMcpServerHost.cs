namespace Coven.Spellcasting.Agents.Codex.MCP;

internal interface IMcpServerSession : IAsyncDisposable
{
    string ToolbeltPath { get; }
}

internal interface IMcpServerHost
{
    Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default);
}
