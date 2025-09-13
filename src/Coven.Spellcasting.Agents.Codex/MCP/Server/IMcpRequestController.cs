// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Coven.Spellcasting.Agents.Codex.MCP.Tools;

namespace Coven.Spellcasting.Agents.Codex.MCP.Server;

internal readonly record struct JsonRpcRequest(string? Id, string Method, JsonElement? Params);

internal sealed class McpStdioContext
{
    public required McpToolbelt Toolbelt { get; init; }
    public required ILogger Logger { get; init; }
    public IMcpSpellExecutorRegistry? Registry { get; init; }
    public required Func<string?, object, CancellationToken, Task> ReplyAsync { get; init; }
    public required Action RequestShutdown { get; init; }
}

internal interface IMcpRequestController
{
    bool CanHandle(string method);
    Task<bool> TryHandleAsync(JsonRpcRequest req, McpStdioContext ctx, CancellationToken ct);
}
