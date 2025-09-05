using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coven.Chat.Tests;

// Scope: Common bookmark/resume patterns (at-least-once semantics)
public class ScrivenerBookmarkTests
{
    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task ResumeFromBookmark_OnlyNewEntries(Func<IScrivener<string>> create)
    {
        var s = create();
        var pA = await s.WriteAsync("A");
        var pB = await s.WriteAsync("B");

        // Consume and set bookmark to last seen
        long bookmark = 0;
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
        {
            await foreach (var (pos, _) in s.TailAsync(0, cts.Token))
            {
                bookmark = pos;
                // We know there are only two entries; stop once we've seen both to avoid cancellation exceptions.
                if (bookmark == pB) break;
            }
        }

        var pC = await s.WriteAsync("C");
        var pD = await s.WriteAsync("D");

        var seen = new List<long>();
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
        {
            await foreach (var (pos, _) in s.TailAsync(bookmark, cts.Token))
            {
                seen.Add(pos);
                if (seen.Count == 2) break;
            }
        }

        Assert.Equal(new[] { pC, pD }, seen);
    }

    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task AtLeastOnce_ReprocessOnNoAdvance(Func<IScrivener<string>> create)
    {
        var s = create();
        var pA = await s.WriteAsync("A");

        // Process but do not advance bookmark (simulate failure)
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            await foreach (var (pos, _) in s.TailAsync(0, cts.Token))
            {
                // do not set bookmark
                break;
            }
        }

        // New consumer resumes from same bookmark (0) and should see A again
        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
        {
            var first = await s.TailAsync(0, cts.Token).GetAsyncEnumerator().MoveNextAsync();
            Assert.True(first);
        }
    }
}
