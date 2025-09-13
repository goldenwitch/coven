// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Spellcasting.Agents.Codex.MCP.Server;

internal sealed class SessionController : IMcpRequestController
{
    private volatile bool _shutdownRequested;

    public bool CanHandle(string method) => method is "initialize" or "shutdown" or "exit";

    public async Task<bool> TryHandleAsync(JsonRpcRequest req, McpStdioContext ctx, CancellationToken ct)
    {
        switch (req.Method)
        {
            case "initialize":
            {
                await ctx.ReplyAsync(req.Id, new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { list = new { }, call = new { } }
                    },
                    serverInfo = new { name = "coven-mcp", version = "0.1" }
                }, ct).ConfigureAwait(false);
                return true;
            }

            case "shutdown":
            {
                _shutdownRequested = true;
                await ctx.ReplyAsync(req.Id, (object?)null!, ct).ConfigureAwait(false);
                return true;
            }

            case "exit":
            {
                if (_shutdownRequested)
                {
                    try { ctx.RequestShutdown(); } catch { /* best effort */ }
                }
                return true; // notification; no reply
            }
        }

        return false;
    }
}
