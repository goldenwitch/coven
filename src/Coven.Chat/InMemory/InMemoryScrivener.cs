// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent; // lock-free container for concurrent producers/consumers
using System.Runtime.CompilerServices; // [EnumeratorCancellation] for IAsyncEnumerable cancellation
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coven.Chat;

/// <summary>
/// In-memory implementation of <see cref="IScrivener{TJournalEntryType}"/> for a single logical stream.
/// Thread-safe for concurrent writers and readers, using an async signal to wake waiters.
/// </summary>
/// <typeparam name="TJournalEntryType">The entry type stored in this stream.</typeparam>
public sealed class InMemoryScrivener<TJournalEntryType> : IScrivener<TJournalEntryType> where TJournalEntryType : notnull
{
    // Append-only buffer holding (position, entry) pairs in approximate FIFO order.
    private readonly ConcurrentQueue<(long pos, TJournalEntryType entry)> _entries = new();
    // Monotonically increasing journal position; starts at 0 so first write becomes 1.
    private long _nextPos = 0; // first assigned will be 1
    // Notification gate used to wake tailers and waiters when a new entry is appended.
    private TaskCompletionSource<bool> _signal = NewSignal();

    private readonly ILogger<InMemoryScrivener<TJournalEntryType>> _log;

    public InMemoryScrivener()
        : this(NullLogger<InMemoryScrivener<TJournalEntryType>>.Instance) { }

    public InMemoryScrivener(ILogger<InMemoryScrivener<TJournalEntryType>> logger)
    {
        _log = logger ?? NullLogger<InMemoryScrivener<TJournalEntryType>>.Instance;
    }

    public Task<long> WriteAsync(TJournalEntryType entry, CancellationToken ct = default)
    {
        // Respect cancellation for symmetry with other async operations.
        ct.ThrowIfCancellationRequested();
        // Defensive: ensure non-null reference entries at runtime (value types unaffected).
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        // Atomically reserve a new journal position.
        var assigned = Interlocked.Increment(ref _nextPos);
        // Append to the concurrent queue (non-blocking for multiple writers).
        _entries.Enqueue((assigned, entry));
        _log.LogDebug("chat:mem write pos={Pos} type={Type}", assigned, entry.GetType().Name);
        // Wake waiters: complete the current signal and rotate to a fresh one.
        var toComplete = Interlocked.Exchange(ref _signal, NewSignal());
        toComplete.TrySetResult(true);
        // Return the assigned position to the caller (anchors future waits).
        return Task.FromResult(assigned);
    }


    public async IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> TailAsync(long afterPosition = 0, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (afterPosition == long.MaxValue)
            yield break; // nothing is after MaxValue

        long next = afterPosition + 1;
        // Buffer for out-of-order items until their turn arrives.
        var pending = new Dictionary<long, TJournalEntryType>();
        _log.LogTrace("chat:mem tail start after={After}", afterPosition);

        while (true)
        {
            var waiter = Volatile.Read(ref _signal);

            // Take a snapshot of entries.
            var snapshot = _entries.ToArray();

            // Accumulate candidates at/after 'next'. Dedup across loops.
            foreach (var (pos, entry) in snapshot)
            {
                if (pos >= next && !pending.ContainsKey(pos))
                    pending[pos] = entry;
            }

            // Emit the longest contiguous prefix starting at 'next'.
            bool progressed = false;
            while (pending.TryGetValue(next, out var entry))
            {
                ct.ThrowIfCancellationRequested();

                yield return (next, entry);
                _log.LogTrace("chat:mem tail yield pos={Pos}", next);
                pending.Remove(next);
                next++;
                progressed = true;
            }

            if (progressed)
                continue;

            _log.LogTrace("chat:mem tail wait pos={Next}", next);
            await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> ReadBackwardAsync(
        long beforePosition = long.MaxValue,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _entries.ToArray();
        Array.Sort(snapshot, (a, b) => b.pos.CompareTo(a.pos));

        await Task.Yield();

        foreach (var (pos, entry) in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (pos < beforePosition)
            {
                yield return (pos, entry);
                _log.LogTrace("chat:mem back yield pos={Pos}", pos);
            }
        }
    }


    public async Task<(long journalPosition, TJournalEntryType entry)> WaitForAsync(
    long afterPosition,
    Func<TJournalEntryType, bool> match,
    CancellationToken ct = default)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (afterPosition == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(afterPosition), "Cannot be long.MaxValue.");

        long next = afterPosition + 1;

        _log.LogTrace("chat:mem wait start after={After}", afterPosition);
        while (true)
        {
            var waiter = Volatile.Read(ref _signal);
            var snapshot = _entries.ToArray();

            // Build a map for O(1) lookup by position within this snapshot.
            var map = new Dictionary<long, TJournalEntryType>();
            foreach (var (pos, entry) in snapshot)
                if (pos >= next) map[pos] = entry;

            bool progressed = false;
            while (map.Remove(next, out var entry))
            {
                ct.ThrowIfCancellationRequested();

                if (match(entry))
                {
                    _log.LogDebug("chat:mem wait match pos={Pos} type={Type}", next, entry?.GetType().Name);
                    return (next, entry);
                }

                next++;      // advance only when we've actually seen 'next'
                progressed = true;
            }

            if (!progressed)
            {
                _log.LogTrace("chat:mem wait sleeping next={Next}", next);
                await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
            }
        }
    }


    public async Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(
        long afterPosition,
        CancellationToken ct = default) where TDerived : TJournalEntryType
    {
        var result = await WaitForAsync(afterPosition, e => e is TDerived, ct).ConfigureAwait(false);
        return (result.journalPosition, (TDerived)(object)result.entry);
    }

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}