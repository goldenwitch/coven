using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices; // For .WithCancellation(...)
using System.Threading;
using System.Threading.Tasks;
using Coven.Chat;
using Coven.Chat.Tests.TestTooling;
using Xunit;

namespace Coven.Chat.Tests;

public class FileScrivener_FailingAndBehaviorTests : FileScrivenerTestBase
{
    [Fact]
    public async Task TailAsync_ShouldNotSkipOverUnreadableEarlierPosition()
    {
        var s = Create<string>();

        await s.WriteAsync("ok1");
        RawJson.WriteRawJsonRecord(Root, 2, typeof(int).AssemblyQualifiedName!, "123"); // unreadable for string
        await s.WriteAsync("ok3");

        using var cts = new CancellationTokenSource(Timeouts.Medium);

        // Recommended pattern: enumerate with await foreach (+ WithCancellation) via a small helper.
        var nextItemTask = ReadFirstAsync(s.TailAsync(afterPosition: 1, cts.Token), cts.Token);

        // With strict contiguity this should NOT complete because pos=2 is unreadable.
        await AsyncAssert.DoesNotCompleteSoon(nextItemTask, Timeouts.Short);

        // Cancel the read and assert clean cancellation.
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => nextItemTask);
    }

    // Minimal helper showing the clean consumption pattern users should follow:
    // enumerate with await foreach + WithCancellation and return the first item.
    private static async Task<(long pos, T entry)> ReadFirstAsync<T>(
        IAsyncEnumerable<(long journalPosition, T entry)> stream,
        CancellationToken ct)
    {
        await foreach (var item in stream.WithCancellation(ct))
            return item;

        throw new InvalidOperationException("Stream completed without yielding an item.");
    }

    [Fact]
    public async Task WaitForAsync_ShouldNotSkipOverUnreadableEarlierPosition()
    {
        var s = Create<string>();

        await s.WriteAsync("ok1");
        RawJson.WriteRawJsonRecord(Root, 2, typeof(int).AssemblyQualifiedName!, "123");
        await s.WriteAsync("ok3");

        using var cts = new CancellationTokenSource(Timeouts.Medium);
        var wait = s.WaitForAsync(afterPosition: 1, match: _ => true, cts.Token);

        // With strict contiguity, this should NOT complete until pos=2 is readable.
        await AsyncAssert.DoesNotCompleteSoon(wait, Timeouts.Short);
    }

    [Fact]
    public async Task WaitForAsync_AfterPositionMaxValue_ShouldThrowArgumentOutOfRange()
    {
        var s = Create<string>();
        using var cts = new CancellationTokenSource(Timeouts.Medium);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => s.WaitForAsync(long.MaxValue, _ => true, cts.Token));
    }

    [Fact]
    public async Task Readers_ShouldNotBlock_Delete_WindowsOnly()
    {
        if (!OS.IsWindows) return;

        var s = Create<string>();

        var pos  = await s.WriteAsync("ok");
        var path = TestPaths.RecordPath(Root, pos);

        // Reader that cooperates with deletion (desired behavior): includes FileShare.Delete.
        await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        Exception? ex = Record.Exception(() => File.Delete(path));

        // Desired behavior: deletion should succeed even with an open, cooperative reader.
        Assert.Null(ex);
    }

    [Fact]
    public async Task FileTailAsync_CancelsPromptly_WhenNoNewFiles()
    {
        var s = Create<string>();
        using var cts = new CancellationTokenSource(Timeouts.Short);

        // Use the same helper; no files means this will cancel promptly.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ReadFirstAsync(s.TailAsync(afterPosition: 0, cts.Token), cts.Token));
    }

    [Fact]
    public async Task TailAsync_WakesOnRename_MoveFromTmpToFinal_Quickly()
    {
        var s = Create<string>();

        using var cts = new CancellationTokenSource(Timeouts.Long);

        // Start the tail first (recommended usage), then perform a write (tmp -> move).
        var firstItemTask = ReadFirstAsync(s.TailAsync(afterPosition: 0, cts.Token), cts.Token);
        var writeTask     = s.WriteAsync("hello");

        // If watcher wiring is wrong, this tends to miss the rename and fall back to the delay.
        var (pos, entry) = await AsyncAssert.CompletesSoon(firstItemTask, ms: 200);
        Assert.Equal(1, pos);
        Assert.Equal("hello", entry);

        await writeTask;
    }

    [Fact]
    public async Task ConcurrentWrites_ProduceContiguousPositions()
    {
        var s = Create<int>();

        const int n = 100;
        var tasks = Enumerable.Range(0, n).Select(i => s.WriteAsync(i)).ToArray();
        var positions = await Task.WhenAll(tasks);

        Array.Sort(positions);
        Assert.Equal(Enumerable.Range(1, n).Select(i => (long)i), positions);
    }

    [Fact]
    public async Task ReadBackwardAsync_Respects_BeforeExclusive()
    {
        var s = Create<int>();

        for (int i = 0; i < 5; i++) await s.WriteAsync(i); // positions 1..5

        var seen = new List<long>();
        await foreach (var (pos, _) in s.ReadBackwardAsync(beforePosition: 4))
        {
            seen.Add(pos);
            if (pos == 1) break;
        }

        Assert.Equal(new long[] { 3, 2, 1 }, seen);
    }
}
