// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Tests.Adapter;

using Coven.Chat;
using Coven.Chat.Tests.Adapter.TestTooling;
using Xunit;

public sealed class ConsoleAdapterDiE2eTests
{

    [Fact]
    public async Task EndToEnd_Console_Input_And_Output()
    {
        // Ingress: simulate user typing
        using var h = ConsoleAdapterDiHarness.Start(o => { o.InputSender = "cli"; });
        h.IO.EnqueueInput("hello coven");

        // Scrivener should receive ChatThought
        var seenThought = await h.Scrivener.WaitForAsync(0, e => e is ChatThought t && t.Text == "hello coven");
        Assert.IsType<ChatThought>(seenThought.entry);

        // Egress: write a response into the journal, expect console output
        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "hi user"));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var delivered = await h.IO.ReadOutputAsync(cts.Token);
        Assert.Equal("hi user", delivered);
    }

    [Fact]
    public async Task Processes_Multiple_Inputs_Through_HostQueue()
    {
        using var h = ConsoleAdapterDiHarness.Start(o => { o.InputSender = "cli"; });
        h.IO.EnqueueInput("line 1");
        h.IO.EnqueueInput("line 2");
        h.IO.EnqueueInput("line 3");

        var seen1 = await h.Scrivener.WaitForAsync(0, e => e is ChatThought t && t.Text == "line 1");
        var seen2 = await h.Scrivener.WaitForAsync(seen1.journalPosition, e => e is ChatThought t && t.Text == "line 2");
        var seen3 = await h.Scrivener.WaitForAsync(seen2.journalPosition, e => e is ChatThought t && t.Text == "line 3");

        Assert.Equal("line 1", ((ChatThought)seen1.entry).Text);
        Assert.Equal("line 2", ((ChatThought)seen2.entry).Text);
        Assert.Equal("line 3", ((ChatThought)seen3.entry).Text);
    }

    [Fact]
    public async Task Delivers_Multiple_Responses_In_Order()
    {
        using var h = ConsoleAdapterDiHarness.Start(o => { o.InputSender = "cli"; });
        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "r1"));
        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "r2"));
        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "r3"));

        using var c1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o1 = await h.IO.ReadOutputAsync(c1.Token);
        using var c2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o2 = await h.IO.ReadOutputAsync(c2.Token);
        using var c3 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o3 = await h.IO.ReadOutputAsync(c3.Token);

        Assert.Equal("r1", o1);
        Assert.Equal("r2", o2);
        Assert.Equal("r3", o3);
    }

    [Fact]
    public void Cancels_Host_Cleans_Up()
    {
        Task run;
        using (var h = ConsoleAdapterDiHarness.Start())
        {
            run = h.HostTask;
        }
        Assert.True(run.IsCompleted);
    }

    [Fact]
    public async Task Interleaves_Input_And_Output_In_Order()
    {
        // 1) User types, then adapter emits thought, then we write a response and expect delivery
        using var h = ConsoleAdapterDiHarness.Start(o => { o.InputSender = "cli"; });
        h.IO.EnqueueInput("ping1");
        var t1 = await h.Scrivener.WaitForAsync(0, e => e is ChatThought th && th.Text == "ping1");
        Assert.Equal("ping1", ((ChatThought)t1.entry).Text);

        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "pong1"));
        using var c1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o1 = await h.IO.ReadOutputAsync(c1.Token);
        Assert.Equal("pong1", o1);

        // 2) Another input arrives before we deliver next response
        h.IO.EnqueueInput("ping2");
        var t2 = await h.Scrivener.WaitForAsync(t1.journalPosition, e => e is ChatThought th && th.Text == "ping2");
        Assert.Equal("ping2", ((ChatThought)t2.entry).Text);

        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "pong2"));
        using var c2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o2 = await h.IO.ReadOutputAsync(c2.Token);
        Assert.Equal("pong2", o2);

        // 3) And one more round to ensure stability
        h.IO.EnqueueInput("ping3");
        var t3 = await h.Scrivener.WaitForAsync(t2.journalPosition, e => e is ChatThought th && th.Text == "ping3");
        Assert.Equal("ping3", ((ChatThought)t3.entry).Text);

        _ = await h.Scrivener.WriteAsync(new ChatResponse("assistant", "pong3"));
        using var c3 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var o3 = await h.IO.ReadOutputAsync(c3.Token);
        Assert.Equal("pong3", o3);
    }

    
}