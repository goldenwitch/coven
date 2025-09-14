// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.MCP.Server;
using Coven.Spellcasting.Agents.Codex.MCP.Tools;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class CapturingConfigWriter : ICodexConfigWriter
{
    public List<(string home, string shim, string pipe, string key)> Calls { get; } = new();
    public void WriteOrMerge(string codexHomeDir, string shimPath, string pipeName, string serverKey = "coven")
        => Calls.Add((codexHomeDir, shimPath, pipeName, serverKey));
}

// Removed obsolete resolver/process factory doubles.

public sealed class FakeMcpServerHost : IMcpServerHost
{
    public McpToolbelt? LastToolbelt { get; private set; }
    public IMcpSpellExecutorRegistry? LastRegistry { get; private set; }
    public int StartCalls { get; private set; }
    public string? LastPipeName { get; private set; }
    private readonly bool _startStdio;

    public FakeMcpServerHost(bool startStdio = false)
    {
        _startStdio = startStdio;
    }

    private sealed class Session : IMcpServerSession
    {
        public string ToolbeltPath { get; }
        public string? PipeName { get; }
        public Session(string toolbeltPath, string? pipeName)
        { ToolbeltPath = toolbeltPath; PipeName = pipeName; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default)
    {
        // No registry specified for this overload
        LastToolbelt = toolbelt; LastRegistry = null; StartCalls++;
        var pipeName = $"coven_mcp_test_{Guid.NewGuid():N}";
        LastPipeName = pipeName;

        if (_startStdio)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var serverStream = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                    await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    var server = new McpStdioServer(toolbelt, serverStream, null);
                    server.Start();
                }
                catch { }
            }, ct);
        }

        IMcpServerSession s = new Session("toolbelt.json", pipeName);
        return Task.FromResult(s);
    }

    public Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, IMcpSpellExecutorRegistry? registry, CancellationToken ct = default)
    {
        LastToolbelt = toolbelt; LastRegistry = registry; StartCalls++;
        var pipeName = $"coven_mcp_test_{Guid.NewGuid():N}";
        LastPipeName = pipeName;

        if (_startStdio)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var serverStream = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                    await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    var server = new McpStdioServer(toolbelt, serverStream, registry);
                    server.Start();
                    // server owns stream lifecycle via its Dispose
                }
                catch { }
            }, ct);
        }

        IMcpServerSession s = new Session("toolbelt.json", pipeName);
        return Task.FromResult(s);
    }
}

// Removed obsolete Noop process factory.

public sealed class CapturingInMemoryTailFactory : ITailMuxFactory
{
    internal InMemoryTailMux? LastInstance { get; private set; }
    public string? LastRolloutPath { get; private set; }
    public string? LastExecutablePath { get; private set; }
    public string? LastWorkspaceDirectory { get; private set; }
    public IReadOnlyList<string>? LastArguments { get; private set; }
    public IReadOnlyDictionary<string, string?>? LastEnvironment { get; private set; }
    public ITailMux Create(
        string documentPath,
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment)
    {
        LastRolloutPath = documentPath;
        LastExecutablePath = executablePath;
        LastWorkspaceDirectory = workingDirectory;
        LastArguments = arguments;
        LastEnvironment = environment;
        LastInstance = new InMemoryTailMux();
        return LastInstance;
    }
}

// Removed obsolete capturing process factory.
