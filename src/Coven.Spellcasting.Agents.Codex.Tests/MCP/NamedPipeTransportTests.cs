using System.IO.Pipes;
using System.Text;
using Coven.Spellcasting.Agents.Codex.MCP;

namespace Coven.Spellcasting.Agents.Codex.Tests.MCP;

public sealed class NamedPipeTransportTests
{
    [Fact]
    public async Task Session_Exposes_Pipe_And_Speaks_Minimal_JsonRpc()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"coven_mcp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var belt = new Coven.Spellcasting.Agents.Codex.MCP.McpToolbelt(new List<Coven.Spellcasting.Agents.Codex.MCP.McpTool>());
            var host = new Coven.Spellcasting.Agents.Codex.MCP.LocalMcpServerHost(workspace);
            await using var session = await host.StartAsync(belt);

            Assert.False(string.IsNullOrWhiteSpace(session.PipeName));

            using var client = new NamedPipeClientStream(".", session.PipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);

            // Build and send a minimal JSON-RPC initialize request with LSP-style framing
            var req = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"initialize\",\"params\":{}}";
            var bytes = Encoding.UTF8.GetBytes(req);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
            await client.WriteAsync(header, 0, header.Length);
            await client.WriteAsync(bytes, 0, bytes.Length);
            await client.FlushAsync();

            // Read response headers
            var contentLength = await ReadContentLengthAsync(client);
            Assert.True(contentLength > 0);
            var body = new byte[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var n = await client.ReadAsync(body, read, contentLength - read);
                Assert.True(n > 0);
                read += n;
            }
            var json = Encoding.UTF8.GetString(body);
            Assert.Contains("\"result\"", json);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { }
        }
    }

    private static async Task<int> ReadContentLengthAsync(Stream s)
    {
        var sb = new StringBuilder();
        var lastWasCr = false;
        int capturedLen = -1;
        var buffer = new byte[1];
        while (true)
        {
            var n = await s.ReadAsync(buffer, 0, 1);
            if (n == 0)
            {
                // EOF without full header terminator; fall back to any captured length
                break;
            }
            var b = (char)buffer[0];
            if (b == '\r') { lastWasCr = true; continue; }
            if (b == '\n')
            {
                var line = sb.ToString();
                sb.Clear();
                if (lastWasCr && line.Length == 0)
                {
                    // end of headers
                    return capturedLen > 0 ? capturedLen : 0;
                }
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var len))
                        capturedLen = len;
                }
                lastWasCr = false;
                continue;
            }
            if (lastWasCr) { sb.Append('\r'); lastWasCr = false; }
            sb.Append(b);
        }
        return capturedLen > 0 ? capturedLen : 0;
    }
}
