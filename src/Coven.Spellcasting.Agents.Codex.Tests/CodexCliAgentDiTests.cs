// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;
using Coven.Spellcasting.Spells;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class CodexCliAgentDiTests
{
    private sealed class EchoIn { public string? Message { get; set; } }
    private sealed class EchoSpell : ISpell<EchoIn, string>
    {
        public Task<string> CastSpell(EchoIn input) => Task.FromResult(input.Message ?? string.Empty);
    }

    [Fact]
    public async Task Wires_Host_Config_And_Tails_Rollout_To_Scrivener()
    {
        var hostDouble = new FakeMcpServerHost();
        var configCapture = new CapturingConfigWriter();
        var tailFactory = new CapturingInMemoryTailFactory();

        using var testHost = new CodexAgentTestHost<ChatEntry>()
            .UseTempWorkspace()
            .Configure(o =>
            {
                o.ExecutablePath = "codex"; // not actually started (NoopProcessFactory)
                o.ShimExecutablePath = "shim.exe";
            })
            .WithHost(hostDouble)
            .WithConfigWriter(configCapture)
            .WithTailFactory(tailFactory)
            .Build();

        var agent = testHost.GetAgent();
        // Provide spells explicitly as contracts for EchoSpell
        var spells0 = new List<ISpellContract> { new EchoSpell() };
        await agent.RegisterSpells(spells0);
        using var cts = new CancellationTokenSource();

        // Kick off the agent in the background
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));

        // Wait a beat to let setup complete
        await Task.Delay(100);

        // The host should have been started with a registry (because spells were provided)
        Assert.True(hostDouble.StartCalls >= 1);
        Assert.NotNull(hostDouble.LastRegistry);

        // Config writer should be invoked with shim + pipe
        // Pipe is set by FakeMcpServerHost as "pipe_test"
        var pipe = hostDouble.LastPipeName;
        Assert.False(string.IsNullOrEmpty(pipe));
        Assert.Contains(configCapture.Calls, c => c.shim == "shim.exe" && c.pipe == pipe);

        // Feed a rollout message and assert scrivener sees translated entry
        var mux = tailFactory.LastInstance ?? throw new InvalidOperationException("Tail mux not created");
        var scrivener = testHost.Services.GetRequiredService<IScrivener<ChatEntry>>();

        // Arrange a waiter before feeding
        var waiter = scrivener.WaitForAsync(0, e => e is ChatResponse r && r.Text.Contains("hello", StringComparison.OrdinalIgnoreCase));

        var line = "{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"hello\"}";
        await mux.FeedAsync(new Line(line, DateTimeOffset.UtcNow));

        // Confirm entry observed, then cancel the agent loop
        var seen = await waiter;
        var chat = (ChatResponse)seen.entry;
        Assert.Contains("hello", chat.Text, StringComparison.OrdinalIgnoreCase);

        cts.Cancel();
        try { await runTask; } catch { }
    }

    [Fact]
    public async Task NoSpells_DoesNot_StartHost_Or_WriteConfig_But_Tails()
    {
        var hostDouble = new FakeMcpServerHost();
        var configCapture = new CapturingConfigWriter();
        var tailFactory = new CapturingInMemoryTailFactory();

        using var testHost = new CodexAgentTestHost<ChatEntry>()
            .UseTempWorkspace()
            .Configure(o =>
            {
                o.ExecutablePath = "codex";
            })
            .WithHost(hostDouble)
            .WithConfigWriter(configCapture)
            .WithTailFactory(tailFactory)
            .Build();

        var agent = testHost.GetAgent();
        using var cts = new CancellationTokenSource();
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));
        await Task.Delay(100);

        Assert.Equal(0, hostDouble.StartCalls);
        Assert.Empty(configCapture.Calls);

        var mux = tailFactory.LastInstance ?? throw new InvalidOperationException("Tail mux not created");
        var scrivener = testHost.Services.GetRequiredService<IScrivener<ChatEntry>>();

        var waiter = scrivener.WaitForAsync(0, e => e is ChatResponse r && r.Text.Contains("$ echo", StringComparison.OrdinalIgnoreCase));
        var cmdLine = "{\"type\":\"command\",\"command\":\"echo hi\",\"cwd\":\"/tmp\"}";
        await mux.FeedAsync(new Line(cmdLine, DateTimeOffset.UtcNow));

        var seen = await waiter;
        var chat2 = (ChatResponse)seen.entry;
        Assert.Contains("$ echo hi", chat2.Text, StringComparison.OrdinalIgnoreCase);

        cts.Cancel();
        try { await runTask; } catch { }
    }

    [Fact]
    public async Task Uses_Deterministic_Rollout_And_Passes_Exec_And_Workspace()
    {
        var tailFactory = new CapturingInMemoryTailFactory();

        using var testHost = new CodexAgentTestHost<ChatEntry>()
            .UseTempWorkspace()
            .Configure(o => o.ExecutablePath = "codex")
            .WithTailFactory(tailFactory)
            .Build();

        var agent = testHost.GetAgent();
        // Provide spells explicitly as contracts
        var spells2 = new List<ISpellContract> { new EchoSpell() };
        await agent.RegisterSpells(spells2);
        using var cts = new CancellationTokenSource();
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));

        await Task.Delay(100);

        Assert.Equal("codex", tailFactory.LastExecutablePath);
        var expectedRollout = Path.Combine(tailFactory.LastWorkspaceDirectory!, ".codex", "codex.rollout.jsonl");
        Assert.Equal(expectedRollout, tailFactory.LastRolloutPath);

        cts.Cancel();
        try { await runTask; } catch { }
    }

    [Fact]
    public async Task WithSpells_But_No_Shim_DoesNot_WriteConfig()
    {
        var hostDouble = new FakeMcpServerHost();
        var configCapture = new CapturingConfigWriter();

        using var testHost = new CodexAgentTestHost<ChatEntry>()
            .UseTempWorkspace()
            .Configure(o =>
            {
                o.ExecutablePath = "codex";
                // No shim provided
            })
            .WithHost(hostDouble)
            .WithConfigWriter(configCapture)
            .WithTailFactory(new CapturingInMemoryTailFactory())
            .Build();

        var agent = testHost.GetAgent();
        // Provide spells explicitly as contracts
        var spells3 = new List<ISpellContract> { new EchoSpell() };
        await agent.RegisterSpells(spells3);
        using var cts = new CancellationTokenSource();
        var runTask = Task.Run(() => agent.InvokeAgent(cts.Token));
        await Task.Delay(100);

        Assert.True(hostDouble.StartCalls >= 1); // tools exist, host started
        Assert.NotNull(hostDouble.LastRegistry);
        Assert.Empty(configCapture.Calls); // no shim => no config write

        cts.Cancel();
        try { await runTask; } catch { }
    }
}
