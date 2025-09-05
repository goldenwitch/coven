using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

namespace Coven.Chat.Tests;

// Scope: Tests that rely on InMemoryScrivener implementation details (reflection injection, positional gaps).
public static class InMemoryScrivenerHarness
{
    private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void InjectEntry<T>(InMemoryScrivener<T> s, long pos, T entry, bool rotateSignal = true) where T : notnull
    {
        var entriesField = typeof(InMemoryScrivener<T>).GetField("_entries", BF)
                          ?? throw new InvalidOperationException("Field _entries not found");
        var queue = (ConcurrentQueue<(long pos, T entry)>)entriesField.GetValue(s)!;

        queue.Enqueue((pos, entry));

        if (rotateSignal)
        {
            var signalField = typeof(InMemoryScrivener<T>).GetField("_signal", BF)
                             ?? throw new InvalidOperationException("Field _signal not found");

            var oldTcs = (TaskCompletionSource<bool>)signalField.GetValue(s)!;
            var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            signalField.SetValue(s, newTcs);
            oldTcs.TrySetResult(true);
        }
    }
}

public class ScrivenerInMemoryConcurrencyTests
{
    private static async Task AssertNotCompletedWithin(Task task, int ms)
    {
        var winner = await Task.WhenAny(task, Task.Delay(ms));
        Assert.NotSame(task, winner);
    }

    [Fact]
    public async Task TailAsync_WaitsForMissingNextPosition_ThenEmitsInOrder()
    {
        var s = new InMemoryScrivener<string>();

        // Publish only position 12 first (gap at 11).
        InMemoryScrivenerHarness.InjectEntry(s, 12, "e12");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Start tailing after position 10: the very next required position is 11.
        var enumerator = s.TailAsync(afterPosition: 10, cts.Token).GetAsyncEnumerator();

        // Should NOT produce yet because 11 is missing.
        var firstMove = enumerator.MoveNextAsync().AsTask();
        await AssertNotCompletedWithin(firstMove, 200);

        // Now publish the missing 11.
        InMemoryScrivenerHarness.InjectEntry(s, 11, "e11");

        // We should now get 11 from the pending MoveNextAsync, then 12 in order.
        Assert.True(await firstMove);
        Assert.Equal(11, enumerator.Current.journalPosition);
        Assert.Equal("e11", enumerator.Current.entry);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(12, enumerator.Current.journalPosition);
        Assert.Equal("e12", enumerator.Current.entry);

        await enumerator.DisposeAsync();
    }

    [Fact]
    public async Task WaitForAsync_DoesNotSkipOverGap_ReturnsFirstMatchInLogOrder()
    {
        var s = new InMemoryScrivener<string>();

        // Inject 11 (non-match), then 12 (match), but leave 10 missing.
        InMemoryScrivenerHarness.InjectEntry(s, 11, "no");
        InMemoryScrivenerHarness.InjectEntry(s, 12, "yes"); // <- matches predicate

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = s.WaitForAsync(afterPosition: 9, match: e => e == "yes", cts.Token);

        // Should NOT complete yet because pos 10 may still arrive and could match.
        await AssertNotCompletedWithin(waitTask, 200);

        // Publish the missing 10 (non-match). Now the first matching by log order is 12.
        InMemoryScrivenerHarness.InjectEntry(s, 10, "no");

        var (pos, entry) = await waitTask;
        Assert.Equal(12, pos);
        Assert.Equal("yes", entry);
    }

    [Fact]
    public async Task ReadBackwardAsync_IsOrderedByPositionDescending()
    {
        var s = new InMemoryScrivener<string>();

        // Insert out of order: 2, 1, 3 (publish order ≠ position order).
        InMemoryScrivenerHarness.InjectEntry(s, 2, "e2");
        InMemoryScrivenerHarness.InjectEntry(s, 1, "e1");
        InMemoryScrivenerHarness.InjectEntry(s, 3, "e3");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var seen = new List<long>();

        await foreach (var (pos, _) in s.ReadBackwardAsync(long.MaxValue, cts.Token))
        {
            seen.Add(pos);
            if (seen.Count == 3) break;
        }

        Assert.Equal(new long[] { 3, 2, 1 }, seen);
    }

    [Fact]
    public async Task TailAsync_MonotonicSequence_UnderConcurrentWrites_WithGapsFilled()
    {
        var s = new InMemoryScrivener<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var producer = Task.Run(async () =>
        {
            InMemoryScrivenerHarness.InjectEntry(s, 5, 5);
            InMemoryScrivenerHarness.InjectEntry(s, 7, 7);
            await Task.Delay(50);
            InMemoryScrivenerHarness.InjectEntry(s, 6, 6);
            InMemoryScrivenerHarness.InjectEntry(s, 4, 4);
            await Task.Delay(50);
            InMemoryScrivenerHarness.InjectEntry(s, 1, 1);
            InMemoryScrivenerHarness.InjectEntry(s, 3, 3);
            InMemoryScrivenerHarness.InjectEntry(s, 2, 2);
        });

        var result = new List<long>();
        await foreach (var (pos, _) in s.TailAsync(afterPosition: 0, cts.Token))
        {
            result.Add(pos);
            if (result.Count == 7) break;
        }

        await producer;

        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6, 7 }, result);
    }
}

