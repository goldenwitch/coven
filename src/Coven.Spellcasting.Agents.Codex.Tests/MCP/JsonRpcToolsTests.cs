using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Coven.Spellcasting.Agents.Codex.MCP;

namespace Coven.Spellcasting.Agents.Codex.Tests.MCP;

public sealed class JsonRpcToolsTests
{
    [Fact]
    public async Task Tools_List_Returns_Toolbelt_Entries()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"coven_mcp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var belt = new McpToolbelt(new List<McpTool>
            {
                new("alpha", "{\"type\":\"object\"}", null),
                new("beta",  null, null)
            });
            var host = new LocalMcpServerHost(workspace);
            await using var session = await host.StartAsync(belt);
            using var client = new NamedPipeClientStream(".", session.PipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);

            // initialize
            await SendJsonRpcAsync(client, "1", "initialize", "{}");
            _ = await ReadJsonRpcAsync(client); // ignore

            // tools/list
            await SendJsonRpcAsync(client, "2", "tools/list", "{}");
            var resp = await ReadJsonRpcAsync(client);
            Assert.NotNull(resp);
            Assert.True(resp!.Value.root.TryGetProperty("result", out var result));
            var tools = result.GetProperty("tools");
            Assert.Equal(2, tools.GetArrayLength());
            var names = tools.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
            Assert.Contains("alpha", names);
            Assert.Contains("beta", names);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { }
        }
    }

    private static async Task SendJsonRpcAsync(Stream s, string id, string method, string jsonParams)
    {
        var json = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{id}\",\"method\":\"{method}\",\"params\":{jsonParams}}}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await s.WriteAsync(header, 0, header.Length);
        await s.WriteAsync(bytes, 0, bytes.Length);
        await s.FlushAsync();
    }

    private static async Task<(string id, JsonDocument doc, JsonElement root)?> ReadJsonRpcAsync(Stream s)
    {
        int len = await ReadContentLengthAsync(s);
        if (len <= 0) return null;
        var buf = new byte[len];
        var read = 0;
        while (read < len)
        {
            var n = await s.ReadAsync(buf, read, len - read);
            if (n <= 0) return null;
            read += n;
        }
        var doc = JsonDocument.Parse(buf);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        return (id, doc, root);
    }

    private static async Task<int> ReadContentLengthAsync(Stream s)
    {
        var sb = new StringBuilder();
        var lastCr = false;
        int capturedLen = -1;
        var buf = new byte[1];
        while (true)
        {
            var n = await s.ReadAsync(buf, 0, 1);
            if (n <= 0) return 0;
            int b = buf[0];
            if (b == '\r') { lastCr = true; continue; }
            if (b == '\n')
            {
                var line = sb.ToString();
                sb.Clear();
                if (lastCr && line.Length == 0)
                {
                    return capturedLen > 0 ? capturedLen : 0;
                }
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var len))
                        capturedLen = len;
                }
                lastCr = false;
                continue;
            }
            if (lastCr) { sb.Append('\r'); lastCr = false; }
            sb.Append((char)b);
        }
    }
}
