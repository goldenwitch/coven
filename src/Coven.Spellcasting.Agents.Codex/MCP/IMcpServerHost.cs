namespace Coven.Spellcasting.Agents.Codex.MCP;

internal interface IMcpServerSession : IAsyncDisposable
{
    IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; }
}

internal interface IMcpServerHost
{
    Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default);
}

