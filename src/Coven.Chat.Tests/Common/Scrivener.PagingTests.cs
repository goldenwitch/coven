using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Coven.Chat.Tests;

// Scope: Common backward-paging pattern (last N → reverse to chronological)
public class ScrivenerPagingTests
{
    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task LastN_BackwardThenReverse_IsChronological(Func<IScrivener<string>> create)
    {
        var s = create();
        var positions = new List<long>();

        for (int i = 1; i <= 10; i++)
        {
            positions.Add(await s.WriteAsync($"e{i}"));
        }

        var n = 5;
        var newestToOldest = new List<(long pos, string val)>();
        await foreach (var (pos, val) in s.ReadBackwardAsync(long.MaxValue))
        {
            newestToOldest.Add((pos, val));
            if (newestToOldest.Count == n) break;
        }

        newestToOldest.Reverse(); // chronological

        // Expect positions to be the last N in increasing order.
        for (int i = 0; i < n; i++)
        {
            var expectedPos = positions[positions.Count - n + i];
            var (pos, val) = newestToOldest[i];
            Assert.Equal(expectedPos, pos);
            Assert.Equal($"e{positions.IndexOf(expectedPos) + 1}", val);
        }
    }
}

