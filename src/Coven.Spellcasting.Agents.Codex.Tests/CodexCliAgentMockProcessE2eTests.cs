// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Processes;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class CodexCliAgentMockProcessE2eTests
{
    private sealed class MockProcessFactory : ICodexProcessFactory
    {
        private sealed class Handle : IProcessHandle
        {
            public Process Process { get; }
            public Handle(Process p) { Process = p; }
            public async ValueTask DisposeAsync()
            {
                try { if (!Process.HasExited) Process.Kill(entireProcessTree: true); } catch { }
                try { await Process.WaitForExitAsync(); } catch { }
                Process.Dispose();
            }
        }

        private readonly string _mockExePath;
        private readonly string _rolloutPath;
        private readonly string _logPath;
        public MockProcessFactory(string mockExePath, string rolloutPath, string logPath)
        { _mockExePath = mockExePath; _rolloutPath = rolloutPath; _logPath = logPath; }

        public IProcessHandle Start(string executablePath, string workingDirectory, IReadOnlyDictionary<string, string?> environment)
        {
            // Start the toy via 'dotnet <dll> --rollout <path>' so stdin is available
            var psi = new ProcessStartInfo(_mockExePath, $"--rollout \"{_rolloutPath}\" --log \"{_logPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true
            };
            // Carry environment
            foreach (var kv in environment)
            {
                if (kv.Value is null) psi.Environment.Remove(kv.Key);
                else psi.Environment[kv.Key] = kv.Value;
            }
            // Ensure PATH is present
            var path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(path)) psi.Environment["PATH"] = path;

            var p = new Process { StartInfo = psi };
            if (!p.Start()) throw new InvalidOperationException("Failed to start MockProcess");
            return new Handle(p);
        }
    }

    [Fact]
    public async Task Agent_Writes_To_Process_And_Reads_Rollout()
    {
        // Resolve the copied mock-process folder under the test output
        var baseDir = AppContext.BaseDirectory;
        var mockDir = Path.Combine(baseDir, "mock-process");
        var mockExe = Path.Combine(mockDir, OperatingSystem.IsWindows() ? "Coven.Toys.MockProcess.exe" : "Coven.Toys.MockProcess");
        Assert.True(File.Exists(mockExe));

        var workspace = Path.Combine(Path.GetTempPath(), $"coven_mock_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var rolloutPath = Path.Combine(workspace, "codex.rollout.jsonl");
        var logPath = Path.Combine(workspace, "sophia.logs.json");

        var services = new ServiceCollection();
        services.AddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();

        // Override process + rollout resolution so we exercise real mux + process
        services.AddSingleton<ICodexProcessFactory>(new MockProcessFactory(mockExe, rolloutPath, logPath));
        services.AddSingleton<IRolloutPathResolver>(new StubRolloutResolver(rolloutPath));

        services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
        {
            o.ExecutablePath = "mock"; // ignored by our process factory
            o.WorkspaceDirectory = workspace;
            o.ShimExecutablePath = null;
        });

        using var sp = services.BuildServiceProvider();
        var scrivener = sp.GetRequiredService<IScrivener<ChatEntry>>();
        var agent = sp.GetRequiredService<ICovenAgent<ChatEntry>>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Send a thought before starting the agent; ingress tails from 0 and will pick it up deterministically
        await scrivener.WriteAsync(new ChatThought("test", "ping"), cts.Token);
        var agentTask = agent.InvokeAgent(cts.Token);

        // Wait for a ChatResponse that contains the echo from the toy
        var (_, entry) = await scrivener.WaitForAsync(
            0,
            e => e is ChatResponse r && r.Text.Contains("echo: ping", StringComparison.OrdinalIgnoreCase),
            cts.Token);
        var resp = (ChatResponse)entry;
        Assert.Contains("echo: ping", resp.Text, StringComparison.OrdinalIgnoreCase);

        // Validate Sophia logs are present and ordered for key events
        await AssertSophiaLogsInOrderAsync(logPath, cts.Token);

        // Cleanup
        cts.Cancel();
        try { await agentTask; } catch { }
    }

    private static async Task AssertSophiaLogsInOrderAsync(string logPath, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        List<string>? entries = null;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(logPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(logPath, ct).ConfigureAwait(false);
                    entries = JsonSerializer.Deserialize<List<string>>(json);
                    if (entries is { Count: > 0 }) break;
                }
                catch { }
            }
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
        Assert.NotNull(entries);
        var logs = entries!;

        int FindAfter(int startIdx, string contains)
        {
            for (int i = Math.Max(0, startIdx + 1); i < logs.Count; i++)
            {
                if (logs[i].Contains(contains, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        var iStart = logs.FindIndex(s => s.Contains("MockProcess starting", StringComparison.OrdinalIgnoreCase));
        Assert.True(iStart >= 0, "Missing 'MockProcess starting' log");

        var iRoll = FindAfter(iStart, "MockProcess rollout path");
        Assert.True(iRoll > iStart, "Missing or out-of-order rollout log");

        var iMeta = FindAfter(iRoll, "metadata written");
        Assert.True(iMeta > iRoll, "Missing or out-of-order metadata log");

        var iStdin = FindAfter(iMeta, "stdin line: ping");
        Assert.True(iStdin > iMeta, "Missing or out-of-order stdin log");

        var iMsg = FindAfter(iStdin, "message written: ping");
        Assert.True(iMsg > iStdin, "Missing or out-of-order message log");
    }
}
