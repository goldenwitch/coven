using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

internal static class TailMuxTestHelpers
{
    internal static async Task<bool> EnsureTailReadyAsync(
        ITailMuxFixture fixture,
        ITestTailMux mux,
        System.Collections.Concurrent.ConcurrentQueue<string> sink,
        string sentinel,
        TimeSpan timeout,
        TimeSpan? retryInterval = null)
    {
        var poll = retryInterval ?? TimeSpan.FromMilliseconds(50);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await fixture.StimulateIncomingAsync(mux, new[] { sentinel });
            var seen = await WaitUntilAsync(() => sink.Contains(sentinel), TimeSpan.FromMilliseconds(200));
            if (seen) return true;
            await Task.Delay(poll);
        }
        return false;
    }
    // Removed NewTempFile helper; fixtures generate and manage paths directly.

    internal static async Task AppendLinesAsync(string path, IEnumerable<string> lines)
    {
        using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var writer = new StreamWriter(fs);
        foreach (var l in lines)
        {
            await writer.WriteLineAsync(l);
            await writer.FlushAsync();
        }
    }

    internal static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var poll = pollInterval ?? TimeSpan.FromMilliseconds(50);
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.Elapsed >= timeout) return false;
            await Task.Delay(poll);
        }
        return true;
    }
}
