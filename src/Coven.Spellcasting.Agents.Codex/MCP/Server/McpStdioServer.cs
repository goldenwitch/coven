// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using System.Text.Json;
using Coven.Spellcasting.Agents.Codex.MCP.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coven.Spellcasting.Agents.Codex.MCP.Server;

/// <summary>
/// Minimal placeholder MCP server using a STDIO-style loop. For now, it simply
/// keeps a background task alive until disposed, and could be extended to speak
/// JSON-RPC per MCP. It retains access to the toolbelt payload for future use.
/// </summary>
    internal sealed class McpStdioServer : IAsyncDisposable
    {
        private readonly McpToolbelt _toolbelt;
        private readonly CancellationTokenSource _cts = new();
        private Task? _serverTask;
        private volatile bool _disposed;
        private readonly Stream _transport;
        private readonly IMcpSpellExecutorRegistry? _registry;
        private readonly ILogger<McpStdioServer> _log = NullLogger<McpStdioServer>.Instance;
        private readonly List<IMcpRequestController> _controllers = new();

    public McpStdioServer(McpToolbelt toolbelt, Stream transport, IMcpSpellExecutorRegistry? registry = null)
    {
        _toolbelt = toolbelt;
        _transport = transport;
        _registry = registry;

        // Register controllers
        _controllers.Add(new SessionController());
        _controllers.Add(new ToolsController());
    }

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpStdioServer));
        if (_serverTask is not null) return;

        _serverTask = Task.Run(() => ServeAsync(_cts.Token), _cts.Token);
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var req = await ReadJsonRpcAsync(ct).ConfigureAwait(false);
                if (req is null) break;
                await DispatchAsync(req.Value, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task DispatchAsync(JsonRpcRequest req, CancellationToken ct)
    {
        try
        {
            var ctx = new McpStdioContext
            {
                Toolbelt = _toolbelt,
                Registry = _registry,
                Logger = _log,
                ReplyAsync = ReplyAsync,
                RequestShutdown = () => { try { _cts.Cancel(); } catch { } }
            };

            // Route to the first controller that claims the method
            foreach (var c in _controllers)
            {
                if (!c.CanHandle(req.Method)) continue;
                var handled = await c.TryHandleAsync(req, ctx, ct).ConfigureAwait(false);
                if (handled) return;
            }

            if (req.Id is not null)
            {
                var err = new { code = -32601, message = $"Method '{req.Method}' not found" };
                await WriteJsonRpcAsync(new { jsonrpc = "2.0", id = req.Id, error = err }, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "MCP request failed for method: {Method}", req.Method);
            if (req.Id is not null)
            {
                var err = new { jsonrpc = "2.0", id = req.Id, error = new { code = -32603, message = ex.Message } };
                await WriteJsonRpcAsync(err, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<JsonRpcRequest?> ReadJsonRpcAsync(CancellationToken ct)
    {
        // LSP-style framing: headers (Content-Length) then CRLFCRLF, then body
        var headers = await ReadHeadersAsync(ct).ConfigureAwait(false);
        if (headers is null) return null;
        if (!headers.TryGetValue("content-length", out var lenStr) || !int.TryParse(lenStr, out var length))
            return null;
        var body = await ReadExactAsync(length, ct).ConfigureAwait(false);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        string? id = null;
        if (root.TryGetProperty("id", out var idEl)) id = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.GetRawText();
        var method = root.GetProperty("method").GetString() ?? string.Empty;
        JsonElement? @params = root.TryGetProperty("params", out var p) ? p : (JsonElement?)null;
        return new JsonRpcRequest(id, method, @params);
    }

    private async Task ReplyAsync(string? id, object result, CancellationToken ct)
    {
        if (id is null) return; // notifications not supported here
        var payload = new { jsonrpc = "2.0", id, result };
        await WriteJsonRpcAsync(payload, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, string>?> ReadHeadersAsync(CancellationToken ct)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        var lastWasCr = false;
        var one = new byte[1];
        while (true)
        {
            int r = await _transport.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (r <= 0) return null;
            int b = one[0];
            if (b == '\r') { lastWasCr = true; continue; }
            if (b == '\n')
            {
                var line = sb.ToString();
                sb.Clear();
                if (lastWasCr && line.Length == 0)
                {
                    break; // end of headers
                }
                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    dict[key] = val;
                }
                lastWasCr = false;
                continue;
            }
            if (lastWasCr)
            {
                // treat stray CR as normal char
                sb.Append('\r');
                lastWasCr = false;
            }
            sb.Append((char)b);
        }
        return dict;
    }

    private async Task<string> ReadExactAsync(int length, CancellationToken ct)
    {
        var buf = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await _transport.ReadAsync(buf.AsMemory(read, length - read), ct).ConfigureAwait(false);
            if (n <= 0) throw new EndOfStreamException();
            read += n;
        }
        return Encoding.UTF8.GetString(buf);
    }

    private async Task WriteJsonRpcAsync(object obj, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _transport.WriteAsync(header.AsMemory(0, header.Length), ct).ConfigureAwait(false);
        await _transport.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        await _transport.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { if (_serverTask is not null) await _serverTask.ConfigureAwait(false); } catch { }
        try { _transport.Dispose(); } catch { }
        _cts.Dispose();
    }
}
