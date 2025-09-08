using System.Collections.Concurrent;
using System.Diagnostics;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public abstract class TailMuxContract<TFixture> : IClassFixture<TFixture> where TFixture : class, ITailMuxFixture
{
    protected TFixture Fixture { get; }
    public TailMuxContract(TFixture fixture) => Fixture = fixture;

    [Fact]
    public async Task TailAsync_Receives_Appended_Lines()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));

        var received = new ConcurrentQueue<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var tailTask = mux.TailAsync((line, isError) =>
        {
            if (!isError) received.Enqueue(line);
            return ValueTask.CompletedTask;
        }, cts.Token);

        await Task.Delay(150);
        await Fixture.StimulateIncomingAsync(mux, new MuxArgs(doc), new[] { "one", "two", "three" });

        var sw = Stopwatch.StartNew();
        while (received.Count < 3 && sw.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(50);

        Assert.Contains("one", received);
        Assert.Contains("two", received);
        Assert.Contains("three", received);

        cts.Cancel();
        await tailTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TailAsync_Single_Reader_Enforced()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var t1 = mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token));

        cts.Cancel();
        await t1.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TailAsync_Can_Restart_After_Cancel()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));

        using (var cts = new CancellationTokenSource())
        {
            var t = mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token);
            await Task.Delay(100);
            cts.Cancel();
            await t.WaitAsync(TimeSpan.FromSeconds(2));
        }

        using (var cts = new CancellationTokenSource())
        {
            var received = 0;
            var t = mux.TailAsync((line, isError) => { if (!isError) received++; return ValueTask.CompletedTask; }, cts.Token);
            await Task.Delay(100);
            await Fixture.StimulateIncomingAsync(mux, new MuxArgs(doc), new[] { "x" });

            var sw = Stopwatch.StartNew();
            while (received < 1 && sw.Elapsed < TimeSpan.FromSeconds(2))
                await Task.Delay(25);

            Assert.Equal(1, received);
            cts.Cancel();
            await t.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task Dispose_Cancels_Tail()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        var mux = Fixture.CreateMux(new MuxArgs(doc));
        var t = mux.TailAsync((line, isError) => ValueTask.CompletedTask);
        await Task.Delay(100);
        await mux.DisposeAsync();
        await t.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Write_After_Dispose_Throws()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));
        await mux.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => mux.WriteLineAsync("hello"));
    }
}

public sealed class ProcessDocumentTailMux_ContractTests : TailMuxContract<ProcessDocumentTailMuxFixture>
{
    public ProcessDocumentTailMux_ContractTests(ProcessDocumentTailMuxFixture fixture) : base(fixture) { }
    [Fact]
    public async Task Write_Does_Not_Require_Tail()
    {
        var doc = TailMuxTestHelpers.NewTempFile();
        await using var mux = Fixture.CreateMux(new MuxArgs(doc));
        await mux.WriteLineAsync("hello world");
        await mux.WriteLineAsync("another line");
    }
}

public sealed class InMemoryTailMux_ContractTests : TailMuxContract<InMemoryTailMuxFixture>
{
    public InMemoryTailMux_ContractTests(InMemoryTailMuxFixture fixture) : base(fixture) { }
    [Fact]
    public async Task Write_Can_Be_Observed_From_Outgoing_Channel()
    {
        await using var mux = Fixture.CreateMux(new MuxArgs("unused"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var collected = new List<string>();
        var readerTask = Task.Run(async () =>
        {
            var underlying = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
            await foreach (var s in underlying.ReadWritesAsync(cts.Token)) collected.Add(s);
        });

        await mux.WriteLineAsync("alpha");
        await mux.WriteLineAsync("beta");

        await Task.Delay(100);
        cts.Cancel();
        await readerTask;

        Assert.Contains("alpha", collected);
        Assert.Contains("beta", collected);
    }
}
