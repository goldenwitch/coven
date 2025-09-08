using System.Collections.Concurrent;
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

        var ok = await TailMuxTestHelpers.WaitUntilAsync(() => received.Count >= 3, TimeSpan.FromSeconds(3));

        Assert.True(ok, "Timed out waiting for 3 lines");
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

            var ok = await TailMuxTestHelpers.WaitUntilAsync(() => received >= 1, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(25));

            Assert.True(ok, "Timed out waiting for 1 line");
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

// Concrete implementations moved to separate files for clarity.
