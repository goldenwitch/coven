using System.Diagnostics;
using Coven.Spellcasting.Agents.Codex.Config;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents.Codex.MCP.Exec;
using Coven.Spellcasting.Agents.Codex.MCP.Stdio;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Tail;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class CapturingConfigWriter : ICodexConfigWriter
{
    public List<(string home, string shim, string pipe, string key)> Calls { get; } = new();
    public void WriteOrMerge(string codexHomeDir, string shimPath, string pipeName, string serverKey = "coven")
        => Calls.Add((codexHomeDir, shimPath, pipeName, serverKey));
}

public sealed class StubRolloutResolver : Rollout.IRolloutPathResolver
{
    private readonly string? _path;
    public StubRolloutResolver(string? path) { _path = path; }
    public Task<string?> ResolveAsync(string codexExecutablePath, string workspaceDirectory, string codexHomeDir, IReadOnlyDictionary<string, string?> env, TimeSpan timeout, CancellationToken ct)
        => Task.FromResult(_path);
}

public sealed class ThrowingProcessFactory : ICodexProcessFactory
{
    public IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment)
        => throw new InvalidOperationException("Process start should not be called in this test.");
}

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
        => StartAsync(toolbelt, null, ct);

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

public sealed class NoopProcessFactory : ICodexProcessFactory
{
    private sealed class Handle : IProcessHandle
    {
        public Process Process { get; }
        public Handle()
        {
            Process = new Process();
        }
        public ValueTask DisposeAsync()
        {
            try { Process.Dispose(); } catch { }
            return ValueTask.CompletedTask;
        }
    }

    public IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment)
        => new Handle();
}

public sealed class CapturingInMemoryTailFactory : ITailMuxFactory
{
    internal InMemoryTailMux? LastInstance { get; private set; }
    public string? LastRolloutPath { get; private set; }
    public Process? LastProcess { get; private set; }
    public ITailMux CreateForRollout(string rolloutPath, Process? process)
    {
        LastRolloutPath = rolloutPath;
        LastProcess = process;
        LastInstance = new InMemoryTailMux();
        return LastInstance;
    }
}

public sealed class CapturingProcessFactory : ICodexProcessFactory
{
    public int StartCalls { get; private set; }
    public string? LastExecutablePath { get; private set; }
    public string? LastWorkingDirectory { get; private set; }
    public IReadOnlyDictionary<string, string?>? LastEnvironment { get; private set; }

    private sealed class Handle : IProcessHandle
    {
        public Process Process { get; }
        public Handle() { Process = new Process(); }
        public ValueTask DisposeAsync() { try { Process.Dispose(); } catch { } return ValueTask.CompletedTask; }
    }

    public IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        StartCalls++;
        LastExecutablePath = executablePath;
        LastWorkingDirectory = workingDirectory;
        LastEnvironment = environment;
        return new Handle();
    }
}
