// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

/// <summary>
/// Abstract contract tests for any <see cref="ITailMux"/> implementation provided by the fixture.
/// Derived classes bind a concrete fixture to run the same behavioral suite across implementations.
/// </summary>
public abstract class TailMuxContract<TFixture> : IClassFixture<TFixture> where TFixture : class, ITailMuxFixture
{
    protected TFixture Fixture { get; }
    public TailMuxContract(TFixture fixture) => Fixture = fixture;

    /// <summary>
    /// When a tail is active and the backing source receives new lines, those lines are emitted to the consumer.
    /// Ensures core tailing behavior functions end-to-end.
    /// </summary>
    [Fact]
    public async Task TailAsync_Receives_Appended_Lines()
    {
        await using var mux = Fixture.CreateMux();

        var received = new ConcurrentQueue<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var tailTask = mux.TailAsync((line, isError) =>
        {
            if (!isError) received.Enqueue(line);
            return ValueTask.CompletedTask;
        }, cts.Token);

        await Fixture.CreateBackingFileAsync(mux);
        const string sentinel = "__ready__";
        var ready = await TailMuxTestHelpers.EnsureTailReadyAsync(Fixture, mux, received, sentinel, TimeSpan.FromSeconds(3));
        Assert.True(ready, "Timed out waiting for tail readiness sentinel");

        await Fixture.StimulateIncomingAsync(mux, new[] { "one", "two", "three" });

        var ok = await TailMuxTestHelpers.WaitUntilAsync(() => received.Count >= 3, TimeSpan.FromSeconds(3));

        Assert.True(ok, "Timed out waiting for 3 lines");
        Assert.Contains("one", received);
        Assert.Contains("two", received);
        Assert.Contains("three", received);

        cts.Cancel();
        await tailTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Enforces that only one active TailAsync reader is allowed at a time, preventing concurrent consumers.
    /// </summary>
    [Fact]
    public async Task TailAsync_Single_Reader_Enforced()
    {
        await using var mux = Fixture.CreateMux();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var t1 = mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token));

        cts.Cancel();
        await t1.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Validates that a tail can be canceled and subsequently restarted, and still receive new lines thereafter.
    /// </summary>
    [Fact]
    public async Task TailAsync_Can_Restart_After_Cancel()
    {
        await using var mux = Fixture.CreateMux();

        using (var cts = new CancellationTokenSource())
        {
            var t = mux.TailAsync((line, isError) => ValueTask.CompletedTask, cts.Token);
            cts.Cancel();
            await t.WaitAsync(TimeSpan.FromSeconds(2));
        }

        using (var cts = new CancellationTokenSource())
        {
            var received = 0;
            const string sentinel = "__ready2__";
            var receivedQ = new ConcurrentQueue<string>();
            var t = mux.TailAsync((line, isError) =>
            {
                if (!isError) { receivedQ.Enqueue(line); if (line != sentinel) received++; }
                return ValueTask.CompletedTask;
            }, cts.Token);
            await Fixture.CreateBackingFileAsync(mux);
            var readyOk = await TailMuxTestHelpers.EnsureTailReadyAsync(Fixture, mux, receivedQ, sentinel, TimeSpan.FromSeconds(3));
            Assert.True(readyOk, "Timed out waiting for tail readiness sentinel (restart)");
            await Fixture.StimulateIncomingAsync(mux, new[] { "x" });

            var ok = await TailMuxTestHelpers.WaitUntilAsync(() => received >= 1, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(25));
            Assert.True(ok, "Timed out waiting for 1 line");

            cts.Cancel();
            await t.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>
    /// Disposing the mux cancels an in-flight tail and allows awaiting its completion promptly.
    /// </summary>
    [Fact]
    public async Task Dispose_Cancels_Tail()
    {
        var mux = Fixture.CreateMux();
        var t = mux.TailAsync((line, isError) => ValueTask.CompletedTask);
        await mux.DisposeAsync();
        await t.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// After disposal, write attempts are rejected with <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public async Task Write_After_Dispose_Throws()
    {
        await using var mux = Fixture.CreateMux();
        await mux.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => mux.WriteAsync("hello"));
    }
}

// Concrete implementations moved to separate files for clarity.
