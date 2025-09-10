using System.Collections.Concurrent;
using System.Threading.Channels;
using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Rollout;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class CodexConsoleStyleAgentTests
{
    private sealed class TestAdapter : IAdapter<ChatEntry>
    {
        private readonly Channel<ChatEntry> _inputs = Channel.CreateUnbounded<ChatEntry>();
        public ConcurrentQueue<string> Outputs { get; } = new();

        public void Enqueue(ChatEntry entry) => _inputs.Writer.TryWrite(entry);

        public async IAsyncEnumerable<ChatEntry> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            while (await _inputs.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_inputs.Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }

        public Task DeliverAsync(ChatEntry entry, CancellationToken ct = default)
        {
            if (entry is ChatResponse r) Outputs.Enqueue(r.Text);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CodexConsoleLike_Wires_Agent_And_Translates_Rollout_To_Chat()
    {
        // Arrange DI similar to CodexConsole
        var services = new ServiceCollection();
        services.AddSingleton<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();
        services.AddSingleton<IAdapterHost<ChatEntry>, SimpleAdapterHost<ChatEntry>>();
        var adapter = new TestAdapter();
        services.AddSingleton<IAdapter<ChatEntry>>(adapter);

        // Use capturing tail factory + no-op process + stub resolver; translator for ChatEntry
        var tailFactory = new CapturingInMemoryTailFactory();
        services.AddSingleton<ITailMuxFactory>(tailFactory);
        services.AddSingleton<Processes.ICodexProcessFactory>(new NoopProcessFactory());
        services.AddSingleton<Rollout.IRolloutPathResolver>(new StubRolloutResolver(path: "ignored"));

        services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
        {
            o.ExecutablePath = "codex";
            o.WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"coven_codexconsole_test_{Guid.NewGuid():N}");
            o.ShimExecutablePath = null; // not needed for this test
        });

        using var sp = services.BuildServiceProvider();
        var scrivener = sp.GetRequiredService<IScrivener<ChatEntry>>();
        var host = sp.GetRequiredService<IAdapterHost<ChatEntry>>();
        var agent = sp.GetRequiredService<ICovenAgent<ChatEntry>>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act: run adapter host and agent concurrently
        var adapterTask = host.RunAsync(scrivener, adapter, cts.Token);
        var agentTask = agent.InvokeAgent(cts.Token);

        // Feed a rollout message through the mux and assert ChatResponse is produced
        await Task.Delay(100); // allow wiring
        var mux = tailFactory.LastInstance ?? throw new InvalidOperationException("Tail mux not created");
        var line = "{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"hello from codex\"}";
        await mux.FeedAsync(new Line(line, DateTimeOffset.UtcNow));

        // Wait until the adapter sees the response delivered
        var ok = await WaitUntilAsync(() => adapter.Outputs.Any(o => o.Contains("hello from codex", StringComparison.OrdinalIgnoreCase)), TimeSpan.FromSeconds(2));
        Assert.True(ok, "Adapter did not receive translated ChatResponse");

        // Cleanup
        cts.Cancel();
        try { await Task.WhenAll(adapterTask, agentTask); } catch { }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? poll = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var interval = poll ?? TimeSpan.FromMilliseconds(25);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(interval);
        }
        return false;
    }
}
