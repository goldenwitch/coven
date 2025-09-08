using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class ProcessDocumentTailMux_ContractTests : TailMuxContract<ProcessDocumentTailMuxFixture>
{
    public ProcessDocumentTailMux_ContractTests(ProcessDocumentTailMuxFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Write_Does_Not_Require_Tail()
    {
        await using var mux = Fixture.CreateMux();
        await mux.WriteLineAsync("hello world");
        await mux.WriteLineAsync("another line");
    }

    [Fact]
    public async Task Tail_Waits_For_File_Created_After_Start_And_Then_Streams()
    {
        await using var mux = Fixture.CreateMux();

        var received = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var errorSeen = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));

        var tailTask = mux.TailAsync((line, isError) =>
        {
            if (isError) errorSeen = true; else received.Enqueue(line);
            return ValueTask.CompletedTask;
        }, cts.Token);

        // Give the tailer a moment to start its loop
        await Task.Delay(150);

        // Act: Ensure the file gets created after tail starts, then send a readiness sentinel, then append.
        await Fixture.CreateBackingFileAsync(mux);
        const string sentinel = "__ready_proc__";
        await Fixture.StimulateIncomingAsync(mux, new[] { sentinel });
        var ready = await TailMuxTestHelpers.WaitUntilAsync(() => received.Contains(sentinel), TimeSpan.FromSeconds(3));
        Assert.True(ready, "Timed out waiting for tail readiness sentinel (proc)");
        await Fixture.StimulateIncomingAsync(mux, new[] { "alpha", "beta", "gamma" });

        // Assert: We eventually observe the appended lines and no errors
        var ok = await TailMuxTestHelpers.WaitUntilAsync(() => received.Count >= 3, TimeSpan.FromSeconds(3));

        Assert.True(ok, "Timed out waiting for lines after file creation");
        Assert.False(errorSeen, "Error lines were observed but not expected");
        Assert.Contains("alpha", received);
        Assert.Contains("beta", received);
        Assert.Contains("gamma", received);

        cts.Cancel();
        await tailTask.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
