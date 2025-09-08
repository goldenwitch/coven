using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.MCP.Exec;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.Tests.MCP;

public sealed class JsonRpcCallExecutionTests
{
    private sealed class EchoIn { public string? Message { get; set; } }
    private sealed class EchoSpell : ISpell<EchoIn, string>
    {
        public Task<string> CastSpell(EchoIn input) => Task.FromResult(input.Message ?? string.Empty);
    }

    private sealed class SquareIn { public int N { get; set; } }
    private sealed class SquareOut { public int Value { get; set; } }
    private sealed class SquareSpell : ISpell<SquareIn, SquareOut>
    {
        public Task<SquareOut> CastSpell(SquareIn input) => Task.FromResult(new SquareOut { Value = input.N * input.N });
    }

    [Fact]
    public async Task Tools_Call_Text_Result()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"coven_mcp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var registry = new ReflectionMcpSpellExecutorRegistry(new object[] { new EchoSpell() });
            var belt = new McpToolbelt(registry.Tools);
            var host = new LocalMcpServerHost(workspace);
            await using var session = await host.StartAsync(belt, registry);

            using var client = new NamedPipeClientStream(".", session.PipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);

            await SendJsonRpcAsync(client, "1", "initialize", "{}");
            _ = await ReadJsonRpcAsync(client);

            // name is derived from TIn friendly name => EchoIn
            var callParams = "{\"name\":\"EchoIn\",\"arguments\":{\"Message\":\"hi\"}}";
            await SendJsonRpcAsync(client, "2", "tools/call", callParams);
            var resp = await ReadJsonRpcAsync(client);
            Assert.NotNull(resp);
            var result = resp!.Value.root.GetProperty("result");
            var content = result.GetProperty("content");
            Assert.Equal(1, content.GetArrayLength());
            var c0 = content[0];
            Assert.Equal("text", c0.GetProperty("type").GetString());
            Assert.Equal("hi", c0.GetProperty("text").GetString());
        }
        finally { try { Directory.Delete(workspace, true); } catch { } }
    }

    [Fact]
    public async Task Tools_Call_Json_Result()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"coven_mcp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var registry = new ReflectionMcpSpellExecutorRegistry(new object[] { new SquareSpell() });
            var belt = new McpToolbelt(registry.Tools);
            var host = new LocalMcpServerHost(workspace);
            await using var session = await host.StartAsync(belt, registry);

            using var client = new NamedPipeClientStream(".", session.PipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);

            await SendJsonRpcAsync(client, "1", "initialize", "{}");
            _ = await ReadJsonRpcAsync(client);

            var callParams = "{\"name\":\"SquareIn\",\"arguments\":{\"N\":7}}";
            await SendJsonRpcAsync(client, "2", "tools/call", callParams);
            var resp = await ReadJsonRpcAsync(client);
            Assert.NotNull(resp);
            var result = resp!.Value.root.GetProperty("result");
            var content = result.GetProperty("content");
            var c0 = content[0];
            Assert.Equal("json", c0.GetProperty("type").GetString());
            var json = c0.GetProperty("json");
            Assert.Equal(49, json.GetProperty("Value").GetInt32());
        }
        finally { try { Directory.Delete(workspace, true); } catch { } }
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
