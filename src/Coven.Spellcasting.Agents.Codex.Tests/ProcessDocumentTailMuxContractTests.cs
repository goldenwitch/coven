using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

/// <summary>
/// Contract test runner bound to the process-backed tail mux that tails a file and writes to a child process.
/// Ensures the shared contract and process/file specific behaviors hold.
/// </summary>
public sealed class ProcessDocumentTailMux_ContractTests : TailMuxContract<ProcessDocumentTailMuxFixture>
{
    public ProcessDocumentTailMux_ContractTests(ProcessDocumentTailMuxFixture fixture) : base(fixture) { }

    /// <summary>
    /// Verifies that posting writes to the child process (stdin) does not require an active tail reader.
    /// </summary>
    [Fact]
    public async Task Write_Does_Not_Require_Tail()
    {
        await using var mux = Fixture.CreateMux();
        await mux.WriteLineAsync("hello world");
        await mux.WriteLineAsync("another line");
    }

    /// <summary>
    /// Validates that the tailer patiently waits for the target file to be created after tail start,
    /// then streams newly appended lines from EOF without errors once it appears.
    /// </summary>
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

        // Act: Ensure the file gets created after tail starts, then send a readiness sentinel (retry until seen), then append.
        await Fixture.CreateBackingFileAsync(mux);
        const string sentinel = "__ready_proc__";
        var ready = await TailMuxTestHelpers.EnsureTailReadyAsync(Fixture, mux, received, sentinel, TimeSpan.FromSeconds(3));
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
