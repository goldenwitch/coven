// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.MCP.Server;

internal sealed class ToolsController : IMcpRequestController
{
    public bool CanHandle(string method) => method is "tools/list" or "tools/call";

    public async Task<bool> TryHandleAsync(JsonRpcRequest req, McpStdioContext ctx, CancellationToken ct)
    {
        switch (req.Method)
        {
            case "tools/list":
            {
                var list = ctx.Toolbelt.Tools;
                var tools = list.Select(t => new
                {
                    name = t.Name,
                    description = (string?)null,
                    inputSchema = ParseSchemaOrNull(t.InputSchema),
                }).ToArray();
                await ctx.ReplyAsync(req.Id, new { tools }, ct).ConfigureAwait(false);
                return true;
            }

            case "tools/call":
            {
                // Expect params: { name: string, arguments: object }
                string? name = null;
                JsonElement? argsEl = null;
                if (req.Params is JsonElement pe && pe.ValueKind == JsonValueKind.Object)
                {
                    if (pe.TryGetProperty("name", out var nEl)) name = nEl.GetString();
                    if (pe.TryGetProperty("arguments", out var aEl)) argsEl = aEl;
                }

                if (!string.IsNullOrWhiteSpace(name) && ctx.Registry is not null &&
                    ctx.Registry.TryInvoke(name!, argsEl, ct, out var task, out var returnsJson))
                {
                    var result = await task.ConfigureAwait(false);
                    object[] content = returnsJson
                        ? new[] { new { type = "json", json = result } }
                        : new[] { new { type = "text", text = result?.ToString() ?? "" } };
                    await ctx.ReplyAsync(req.Id, new { content }, ct).ConfigureAwait(false);
                }
                else
                {
                    var content = new object[] { new { type = "text", text = $"tool '{name ?? "?"}' not implemented" } };
                    await ctx.ReplyAsync(req.Id, new { content }, ct).ConfigureAwait(false);
                }
                return true;
            }
        }

        return false;
    }

    private static object? ParseSchemaOrNull(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return null;
        using var doc = JsonDocument.Parse(schema);
        return doc.RootElement.Clone();
    }
}
