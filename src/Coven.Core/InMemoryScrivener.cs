// SPDX-License-Identifier: BUSL-1.1
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Coven.Core;

// ============================================================================
// Simplification notes (v0.1)
// ----------------------------------------------------------------------------
// A) [Implemented] Replace ConcurrentQueue+reordering with a single lock-protected List<(long,TEntry)>.
//    - Pros: TailAsync drops the 'pending' map & snapshot copies; ReadBackwardAsync can
//      just walk the list backwards; WaitForAsync can scan from a cursor (O(delta)).
//    - Cons: Writes contend on a lock; throughput lower, but acceptable for single-process,
//      in-memory semantics.
//
// B) [Implemented] Use an async-friendly signal primitive (Channel<T>) instead of TCS swapping.
//    - Options: AsyncAutoResetEvent-like primitive, or Channel<T>. With Channel<T>, TailAsync
//      is trivial. Keep a List for snapshots/backward scans.
//
// C) Avoid repeated array allocations: enumerate ConcurrentQueue<T> directly rather than
//    materializing to an array each loop (ToArray / [.. _entries]). Enumeration of
//    ConcurrentQueue<T> is a thread-safe snapshot.
//
// E) Micro-optimizations (if hot):
//    - Track 'scannedUpTo' across waiter loops to avoid rescanning the same prefix.
//    - Check cancellation earlier in loops.
//
// F) Optional API tweaks:
//    - Return ValueTask<long> from WriteAsync if you want to avoid Task allocation (be mindful
//      of ValueTask pitfalls).
//    - Add bounded capacity (ring buffer) to cap memory and expose Count/LatestPosition.
//    - Add Complete() to end tailers gracefully (set a completed flag and signal waiters).
// ============================================================================

/// <summary>
/// In-memory implementation of IScrivener<T> journal with simple, single-process semantics;
/// supports tailing, backward read, and predicate waits.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public sealed class InMemoryScrivener<TEntry> : IScrivener<TEntry> where TEntry : notnull
{
    // Implemented A: lock-protected List ensures pos assignment + append are atomic and in-order.
    private readonly Lock _gate = new();
    private readonly List<(long pos, TEntry entry)> _entries = [];

    private long _nextPos; // first assigned will be 1

    // Simplify B: consider AsyncAutoResetEvent/Channel<T> instead of TCS exchange to reduce allocations and clarify intent.
    // Implemented B: level-triggered readiness via bounded Channel (capacity=1) to coalesce multiple writes.
    private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    // Simplify F: could be ValueTask<long> (sync-completed) if high-frequency; weigh ValueTask complexities first.
    public Task<long> WriteAsync(TEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        long pos;
        lock (_gate)
        {
            pos = ++_nextPos; // assign and append atomically under the lock
            _entries.Add((pos, entry));
        }

        // Notify waiters: write a token; multiple writes coalesce while buffer is full.
        _signal.Writer.TryWrite(true);

        return Task.FromResult(pos);
    }

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (afterPosition == long.MaxValue)
        {
            yield break;
        }

        long next = afterPosition + 1;

        while (true)
        {
            ChannelReader<bool> reader = _signal.Reader;
            // Reset level (best-effort) so subsequent WaitToReadAsync will truly wait if no new writes arrive.
            reader.TryRead(out _);

            bool progressed = false;
            while (true)
            {
                (long pos, TEntry entry) item;
                lock (_gate)
                {
                    int idx = (int)(next - 1);
                    if (idx >= 0 && idx < _entries.Count)
                    {
                        item = _entries[idx];
                    }
                    else
                    {
                        break;
                    }
                }

                ct.ThrowIfCancellationRequested();
                yield return (item.pos, item.entry);
                next = item.pos + 1;
                progressed = true;
            }

            if (progressed)
            {
                continue;
            }

            await reader.WaitToReadAsync(ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, [EnumeratorCancellation] CancellationToken ct = default)
    {
        (long pos, TEntry entry)[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _entries];
        }

        await Task.Yield();

        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            (long pos, TEntry? entry) = snapshot[i];
            if (pos < beforePosition)
            {
                yield return (pos, entry);
            }
        }
    }

    public async Task<(long journalPosition, TEntry entry)> WaitForAsync(long afterPosition, Func<TEntry, bool> match, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        long scanPos = afterPosition + 1;

        while (true)
        {
            ChannelReader<bool> reader = _signal.Reader;
            // Reset level (best-effort) so subsequent WaitToReadAsync will truly wait if no new writes arrive.
            reader.TryRead(out _);

            int count;
            lock (_gate)
            {
                count = _entries.Count;
            }

            for (long p = scanPos; p <= count; p++)
            {
                (long pos, TEntry entry) item;
                lock (_gate)
                {
                    item = _entries[(int)(p - 1)];
                }

                if (match(item.entry))
                {
                    ct.ThrowIfCancellationRequested();
                    return (item.pos, item.entry);
                }
            }

            scanPos = count + 1;
            await reader.WaitToReadAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken ct = default) where TDerived : TEntry
    {
        ArgumentNullException.ThrowIfNull(match);
        (long pos, TEntry? e) = await WaitForAsync(afterPosition, e => e is TDerived d && match(d), ct).ConfigureAwait(false);
        return (pos, (TDerived)e);
    }
}
