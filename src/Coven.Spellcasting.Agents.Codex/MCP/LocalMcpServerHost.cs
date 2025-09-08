using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents.Codex.MCP.Stdio;
using Coven.Spellcasting.Agents.Codex.MCP.Exec;
using System.IO.Pipes;

namespace Coven.Spellcasting.Agents.Codex.MCP;

internal sealed class LocalMcpServerHost : IMcpServerHost
{
    private readonly string _workspaceDirectory;

    public LocalMcpServerHost(string workspaceDirectory)
    {
        _workspaceDirectory = workspaceDirectory;
    }

    public async Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default)
        => await StartAsync(toolbelt, null, ct).ConfigureAwait(false);

    public async Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, IMcpSpellExecutorRegistry? registry, CancellationToken ct = default)
    {
        // For now, model a lightweight, disposable session that:
        // - writes the toolbelt to a temp file under the workspace
        // - exposes an env var for the codex process (future client reads/bridges this)
        // Actual MCP wire protocol bridging can be added behind the same interface.

        var mcpDir = Path.Combine(_workspaceDirectory, ".coven-mcp");
        try { Directory.CreateDirectory(mcpDir); } catch { }

        var toolFile = Path.Combine(mcpDir, $"toolbelt-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(toolFile, toolbelt.ToJson(), ct).ConfigureAwait(false);

        // Create a named pipe for the shim to connect to; once a client connects,
        // run the stdio server over that stream.
        var pipeName = $"coven_mcp_{Guid.NewGuid():N}";
        var handshakeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        // Accept a one-shot handshake connection (PING/PONG), then accept the real client and start the server.
        _ = Task.Run(async () =>
        {
            try
            {
                await handshakeServer.WaitForConnectionAsync(ct).ConfigureAwait(false);
                await PerformHandshakeAsync(handshakeServer, ct).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                try { handshakeServer.Dispose(); } catch { }
            }

            // Now create the actual server pipe to accept the shim for the full session
            var serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var server = new McpStdioServer(toolbelt, serverStream, registry);
                server.Start();
            }
            catch { try { serverStream.Dispose(); } catch { } }
        }, ct);

        // Proactively perform client-side handshake to ensure readiness before returning
        await TryClientHandshakeAsync(pipeName, ct).ConfigureAwait(false);

        return new LocalSession(toolFile, pipeName);
    }

    private sealed class LocalSession : IMcpServerSession
    {
        public string ToolbeltPath { get; }
        public string? PipeName { get; }

        public LocalSession(string toolFile, string pipeName)
        {
            ToolbeltPath = toolFile;
            PipeName = pipeName;
        }

        public ValueTask DisposeAsync()
        {
            try { if (File.Exists(ToolbeltPath)) File.Delete(ToolbeltPath); } catch { }
            return ValueTask.CompletedTask;
        }
    }

    private static async Task PerformHandshakeAsync(Stream s, CancellationToken ct)
    {
        var buf = new byte[4];
        int read = await s.ReadAsync(buf.AsMemory(0, 4), ct).ConfigureAwait(false);
        if (read == 4 && buf[0] == (byte)'P' && buf[1] == (byte)'I' && buf[2] == (byte)'N' && buf[3] == (byte)'G')
        {
            await s.WriteAsync(new byte[] { (byte)'P', (byte)'O', (byte)'N', (byte)'G' }, ct).ConfigureAwait(false);
            await s.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task TryClientHandshakeAsync(string pipeName, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(500, ct).ConfigureAwait(false);
                await client.WriteAsync(new byte[] { (byte)'P', (byte)'I', (byte)'N', (byte)'G' }, ct).ConfigureAwait(false);
                await client.FlushAsync(ct).ConfigureAwait(false);
                var resp = new byte[4];
                int n = await client.ReadAsync(resp.AsMemory(0, 4), ct).ConfigureAwait(false);
                if (n == 4 && resp[0] == (byte)'P' && resp[1] == (byte)'O' && resp[2] == (byte)'N' && resp[3] == (byte)'G')
                {
                    return;
                }
            }
            catch { }
            try { await Task.Delay(100, ct).ConfigureAwait(false); } catch { break; }
        }
    }
}
