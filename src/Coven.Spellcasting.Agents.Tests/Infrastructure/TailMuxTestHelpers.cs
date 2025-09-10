using System.Diagnostics;

namespace Coven.Spellcasting.Agents.Tests.Infrastructure;

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

