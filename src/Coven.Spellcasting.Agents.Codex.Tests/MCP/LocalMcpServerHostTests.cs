using System.Text.Json;
using Coven.Spellcasting.Agents.Codex.MCP;

namespace Coven.Spellcasting.Agents.Codex.Tests.MCP;

public sealed class LocalMcpServerHostTests
{
    [Fact]
    public async Task StartAsync_Writes_Toolbelt_And_Exposes_Path()
    {
        var workspace = CreateTempDir();
        try
        {
            var tools = new List<McpTool>
            {
                new("alpha", "{\"type\":\"object\"}", null),
                new("beta",  null,                          "{\"type\":\"string\"}")
            };
            var belt = new McpToolbelt(tools);

            var host = new LocalMcpServerHost(workspace);
            await using var session = await host.StartAsync(belt);

            var path = session.ToolbeltPath;
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));
            Assert.StartsWith(Path.Combine(workspace, ".coven-mcp"), Path.GetDirectoryName(path)!, StringComparison.OrdinalIgnoreCase);

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var toolsArr = doc.RootElement.GetProperty("tools");
            Assert.Equal(2, toolsArr.GetArrayLength());
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task Dispose_Deletes_Toolbelt_File()
    {
        var workspace = CreateTempDir();
        try
        {
            var belt = new McpToolbelt(new List<McpTool>());
            var host = new Coven.Spellcasting.Agents.Codex.MCP.LocalMcpServerHost(workspace);
            string? path;
            await using (var session = await host.StartAsync(belt))
            {
                path = session.ToolbeltPath;
                Assert.NotNull(path);
                Assert.True(File.Exists(path!));
            }

            Assert.False(File.Exists(path!));
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task Multiple_Sessions_Create_Distinct_Files()
    {
        var workspace = CreateTempDir();
        try
        {
            var belt = new McpToolbelt(new List<McpTool>());
            var host = new Coven.Spellcasting.Agents.Codex.MCP.LocalMcpServerHost(workspace);

            string? p1;
            await using (var s1 = await host.StartAsync(belt))
            {
                p1 = s1.ToolbeltPath;
                Assert.True(File.Exists(p1!));
                string? p2;
                await using (var s2 = await host.StartAsync(belt))
                {
                    p2 = s2.ToolbeltPath;
                    Assert.True(File.Exists(p2!));
                    Assert.NotEqual(p1, p2);
                }
                Assert.False(File.Exists(p2!));
            }
            Assert.False(File.Exists(p1!));
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"coven_mcp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
