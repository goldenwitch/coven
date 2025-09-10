using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console;
using Coven.Chat.Tests.Adapter.TestTooling;
using Coven.Chat.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Coven.Chat.Tests;

public class BreadcrumbLoggingTests
{
    private readonly ITestOutputHelper output;
    public BreadcrumbLoggingTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public async Task Chat_Breadcrumbs_Reflect_Ingress_Then_Egress()
    {
        var provider = new InMemoryLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(provider));

        var io = new FakeConsoleIO();
        var opts = new ConsoleAdapterOptions { InputSender = "cli", EchoUserInput = false };
        var adapter = new ConsoleAdapter(io, opts, loggerFactory.CreateLogger<ConsoleAdapter>());
        var scrivener = new InMemoryScrivener<ChatEntry>(loggerFactory.CreateLogger<InMemoryScrivener<ChatEntry>>());
        var host = new SimpleAdapterHost<ChatEntry>(loggerFactory.CreateLogger<SimpleAdapterHost<ChatEntry>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = host.RunAsync(scrivener, adapter, cts.Token);

        // Ingress: enqueue a user thought
        io.EnqueueInput("hello breadcrumb");
        var seenThought = await scrivener.WaitForAsync(0, e => e is ChatThought t && t.Text == "hello breadcrumb", cts.Token);
        Assert.IsType<ChatThought>(seenThought.entry);

        // Egress: write a response, expect adapter to deliver
        _ = await scrivener.WriteAsync(new ChatResponse("assistant", "ok"), cts.Token);
        var delivered = await io.ReadOutputAsync(cts.Token);
        Assert.Equal("ok", delivered);

        // End the chat session to ensure end/cancel breadcrumbs are emitted
        cts.Cancel();
        try { await run; } catch { }

        // Snapshot log lines then filter to chat category
        var lines = ((InMemoryLoggerProvider)provider).Entries.ToList();
        var chatLines = lines.Where(l => l.Contains("Coven.Chat", StringComparison.Ordinal)).ToList();
        for (int i = 0; i < chatLines.Count; i++)
        {
            output.WriteLine($"chat[{i}] {chatLines[i]}");
        }
        // Find begin and extract cid
        var beginLine = chatLines.FirstOrDefault(l => l.Contains("Chat begin cid=", StringComparison.Ordinal));
        Assert.NotNull(beginLine);
        var cidStart = beginLine!.IndexOf("cid=", StringComparison.Ordinal) + 4;
        var cidEnd = beginLine.IndexOf(' ', cidStart);
        var cid = cidEnd > cidStart ? beginLine.Substring(cidStart, cidEnd - cidStart) : beginLine.Substring(cidStart);

        // Filter to this cid
        var conv = chatLines.Where(l => l.Contains($"cid={cid}", StringComparison.Ordinal)).ToList();

        // Assert ordering: begin -> ingress append -> egress deliver -> end
        int idxBegin = conv.FindIndex(s => s.Contains("Chat begin", StringComparison.Ordinal));
        int idxIngress = conv.FindIndex(s => s.Contains("Ingress append", StringComparison.Ordinal));
        int idxEgress = conv.FindIndex(s => s.Contains("Egress deliver", StringComparison.Ordinal));
        int idxEnd = conv.FindIndex(s => s.Contains("Chat end", StringComparison.Ordinal));

        Assert.True(idxBegin >= 0, "Missing chat begin breadcrumb");
        Assert.True(idxIngress > idxBegin, "Ingress append should follow begin");
        Assert.True(idxEgress > idxIngress, "Egress deliver should follow ingress append");
        Assert.True(idxEnd > idxEgress, "Chat end should follow egress deliver");

    }
}
