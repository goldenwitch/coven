using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Coven.Spellcasting.Agents.Codex.MCP;

internal sealed class LocalMcpServerHost : IMcpServerHost
{
    private readonly string _workspaceDirectory;

    public LocalMcpServerHost(string workspaceDirectory)
    {
        _workspaceDirectory = workspaceDirectory;
    }

    public async Task<IMcpServerSession> StartAsync(McpToolbelt toolbelt, CancellationToken ct = default)
    {
        // For now, model a lightweight, disposable session that:
        // - writes the toolbelt to a temp file under the workspace
        // - exposes an env var for the codex process (future client reads/bridges this)
        // Actual MCP wire protocol bridging can be added behind the same interface.

        var mcpDir = Path.Combine(_workspaceDirectory, ".coven-mcp");
        try { Directory.CreateDirectory(mcpDir); } catch { }

        var toolFile = Path.Combine(mcpDir, $"toolbelt-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(toolFile, toolbelt.ToJson(), ct).ConfigureAwait(false);

        var env = new Dictionary<string, string?>
        {
            // Covenant-specific hint for downstream integration. Codex wiring may map this later.
            ["COVEN_MCP_TOOLBELT"] = toolFile
        };

        return new LocalSession(env, toolFile);
    }

    private sealed class LocalSession : IMcpServerSession
    {
        public IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; }
        private readonly string _toolFile;

        public LocalSession(IReadOnlyDictionary<string, string?> env, string toolFile)
        {
            EnvironmentOverrides = env;
            _toolFile = toolFile;
        }

        public ValueTask DisposeAsync()
        {
            try { if (File.Exists(_toolFile)) File.Delete(_toolFile); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}

