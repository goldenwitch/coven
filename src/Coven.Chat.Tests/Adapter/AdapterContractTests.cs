// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Chat.Tests.Adapter;

using Coven.Chat;
using Coven.Chat.Adapter;
using Xunit;

public abstract class AdapterContractTests : IDisposable
{
    protected readonly IScrivener<ChatEntry> Scrivener = new InMemoryScrivener<ChatEntry>();
    protected readonly IAdapterHost<ChatEntry> Host = new SimpleAdapterHost<ChatEntry>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _run;

    protected AdapterContractTests()
    {
        _run = Host.RunAsync(Scrivener, CreateAdapter(), _cts.Token);
    }

    protected abstract IAdapter<ChatEntry> CreateAdapter();

    // Provider-specific hooks
    protected abstract Task ProduceInboundAsync(string text);
    protected abstract Task<string?> TryConsumeOutboundAsync(TimeSpan timeout);

    [Fact]
    public async Task Ingress_Writes_Thought_To_Scrivener()
    {
        await ProduceInboundAsync("hello world");
        var seen = await Scrivener.WaitForAsync(0, e => e is ChatThought t && t.Text == "hello world");
        Assert.IsType<ChatThought>(seen.entry);
        Assert.Equal("hello world", ((ChatThought)seen.entry).Text);
    }

    [Fact]
    public async Task Egress_Writes_Response_To_Transport()
    {
        _ = await Scrivener.WriteAsync(new ChatResponse("assistant", "hi there"));
        var delivered = await TryConsumeOutboundAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("hi there", delivered);
    }

    [Fact]
    public async Task Egress_Ignores_Thought_By_Default()
    {
        _ = await Scrivener.WriteAsync(new ChatThought("user", "should not echo"));
        var delivered = await TryConsumeOutboundAsync(TimeSpan.FromMilliseconds(200));
        Assert.Null(delivered);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _run.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }
}
