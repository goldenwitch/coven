using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;
using Coven.Spellcasting.Spells;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class AgentMcpIntegrationTests
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
    public async Task Agent_Starts_McpServer_And_Executes_Tool_Call()
    {
        var hostDouble = new FakeMcpServerHost(startStdio: true);

        using var testHost = new CodexAgentTestHost<string>()
            .UseTempWorkspace()
            .Configure(o =>
            {
                o.ExecutablePath = "codex";
                o.ShimExecutablePath = "shim.exe"; // config write path; not asserted here
                o.Spells = new object[] { new EchoSpell() };
            })
            .WithHost(hostDouble)
            .WithProcessFactory(new NoopProcessFactory())
            .WithTailFactory(new CapturingInMemoryTailFactory())
            .WithRolloutResolver(new StubRolloutResolver(path: "ignored"))
            .Build();

        var agent = testHost.GetAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));

        // Wait for host to be started and pipe created
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (hostDouble.LastPipeName is null && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.False(string.IsNullOrEmpty(hostDouble.LastPipeName));

        using var client = new NamedPipeClientStream(".", hostDouble.LastPipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(2000);

        await SendJsonRpcAsync(client, "1", "initialize", "{}");
        _ = await ReadJsonRpcAsync(client); // ignore

        // name derived from input type friendly name => EchoIn
        var callParams = "{\"name\":\"EchoIn\",\"arguments\":{\"Message\":\"hello-agent\"}}";
        await SendJsonRpcAsync(client, "2", "tools/call", callParams);
        var resp = await ReadJsonRpcAsync(client);
        Assert.NotNull(resp);
        var result = resp!.Value.root.GetProperty("result");
        var content = result.GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        var c0 = content[0];
        Assert.Equal("text", c0.GetProperty("type").GetString());
        Assert.Equal("hello-agent", c0.GetProperty("text").GetString());

        cts.Cancel();
        try { await runTask; } catch { }
    }

    [Fact]
    public async Task Agent_Executes_Tool_Call_With_Json_Result()
    {
        var hostDouble = new FakeMcpServerHost(startStdio: true);

        using var testHost = new CodexAgentTestHost<string>()
            .UseTempWorkspace()
            .Configure(o =>
            {
                o.ExecutablePath = "codex";
                o.Spells = new object[] { new SquareSpell() };
            })
            .WithHost(hostDouble)
            .WithProcessFactory(new NoopProcessFactory())
            .WithTailFactory(new CapturingInMemoryTailFactory())
            .WithRolloutResolver(new StubRolloutResolver(path: "ignored"))
            .Build();

        var agent = testHost.GetAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (hostDouble.LastPipeName is null && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.False(string.IsNullOrEmpty(hostDouble.LastPipeName));

        using var client = new NamedPipeClientStream(".", hostDouble.LastPipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(2000);

        await SendJsonRpcAsync(client, "1", "initialize", "{}");
        _ = await ReadJsonRpcAsync(client);

        var callParams = "{\"name\":\"SquareIn\",\"arguments\":{\"N\":7}}";
        await SendJsonRpcAsync(client, "2", "tools/call", callParams);
        var resp = await ReadJsonRpcAsync(client);
        Assert.NotNull(resp);
        var result = resp!.Value.root.GetProperty("result");
        var content = result.GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        var c0 = content[0];
        Assert.Equal("json", c0.GetProperty("type").GetString());
        var json = c0.GetProperty("json");
        Assert.Equal(49, json.GetProperty("Value").GetInt32());

        cts.Cancel();
        try { await runTask; } catch { }
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
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) return 0;
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
