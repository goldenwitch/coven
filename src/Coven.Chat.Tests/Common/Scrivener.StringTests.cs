using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Coven.Chat.Tests.TestTooling;

namespace Coven.Chat.Tests;

// Scope: Common behavior tests that must hold for ANY IScrivener<string> implementation.
public static class StringScrivenerFactories
{
    public static IEnumerable<object[]> Create()
        => ScrivenerFactory.Both<string>("Coven_FileScrivener_Common_String_");
}

public class ScrivenerStringTests
{
    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task WriteAndTailForward_YieldsEntriesInOrder(Func<IScrivener<string>> create)
    {
        var s = create();
        var p1 = await s.WriteAsync("a");
        var p2 = await s.WriteAsync("b");

        var result = new List<(long pos, string val)>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await foreach (var (pos, val) in s.TailAsync(0, cts.Token))
        {
            result.Add((pos, val));
            if (result.Count == 2) break;
        }

        Assert.Collection(result,
            i => { Assert.Equal(p1, i.pos); Assert.Equal("a", i.val); },
            i => { Assert.Equal(p2, i.pos); Assert.Equal("b", i.val); });
    }

    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task ReadBackward_ReturnsDescendingBeforePosition(Func<IScrivener<string>> create)
    {
        var s = create();
        var p1 = await s.WriteAsync("a");
        var p2 = await s.WriteAsync("b");
        var p3 = await s.WriteAsync("c");

        var result = new List<(long pos, string val)>();
        await foreach (var (pos, val) in s.ReadBackwardAsync(long.MaxValue))
        {
            result.Add((pos, val));
        }

        Assert.Collection(result,
            i => { Assert.Equal(p3, i.pos); Assert.Equal("c", i.val); },
            i => { Assert.Equal(p2, i.pos); Assert.Equal("b", i.val); },
            i => { Assert.Equal(p1, i.pos); Assert.Equal("a", i.val); });

        var filtered = new List<(long pos, string val)>();
        await foreach (var (pos, val) in s.ReadBackwardAsync(p3))
        {
            filtered.Add((pos, val));
        }
        Assert.Collection(filtered,
            i => { Assert.Equal(p2, i.pos); Assert.Equal("b", i.val); },
            i => { Assert.Equal(p1, i.pos); Assert.Equal("a", i.val); });
    }

    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task WaitForAsync_ReturnsFirstMatchAfterAnchor(Func<IScrivener<string>> create)
    {
        var s = create();
        var anchor = await s.WriteAsync("alpha");
        var waiter = s.WaitForAsync(anchor, e => e.StartsWith("b"));
        await s.WriteAsync("aardvark");
        var pb = await s.WriteAsync("beta");
        var (pos, entry) = await waiter;
        Assert.Equal(pb, pos);
        Assert.Equal("beta", entry);
    }

    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task TailAsync_BlocksUntilNewEntryThenYields(Func<IScrivener<string>> create)
    {
        var s = create();
        var anchor = await s.WriteAsync("seed");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var collected = new List<(long pos, string val)>();

        var readerTask = Task.Run(async () =>
        {
            await foreach (var (pos, val) in s.TailAsync(anchor, cts.Token))
            {
                collected.Add((pos, val));
                break;
            }
        });

        await Task.Delay(10);
        var p2 = await s.WriteAsync("next");
        await readerTask;

        Assert.Single(collected);
        Assert.Equal((p2, "next"), collected[0]);
    }

    [Theory]
    [MemberData(nameof(StringScrivenerFactories.Create), MemberType = typeof(StringScrivenerFactories))]
    public async Task WaitForAsync_RespectsCancellation(Func<IScrivener<string>> create)
    {
        var s = create();
        var anchor = await s.WriteAsync("seed");
        using var cts = new CancellationTokenSource();
        var task = s.WaitForAsync(anchor, _ => false, cts.Token);
        cts.CancelAfter(20);
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }
}
