// SPDX-License-Identifier: BUSL-1.1
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Coven.Core;

/// <summary>
/// In-memory implementation of IScrivener<T> journal with simple, single-process semantics supports tailing, backward read, and predicate waits.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public sealed class InMemoryScrivener<TEntry> : IScrivener<TEntry>, IDisposable where TEntry : notnull
{
    private readonly ConcurrentQueue<(long pos, TEntry entry)> _entries = new();
    private long _nextPos; // first assigned will be 1
    private TaskCompletionSource<bool> _signal = NewSignal();

    public Task<long> WriteAsync(TEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        long pos = Interlocked.Increment(ref _nextPos);
        _entries.Enqueue((pos, entry));
        TaskCompletionSource<bool> toComplete = Interlocked.Exchange(ref _signal, NewSignal());
        toComplete.TrySetResult(true);
        return Task.FromResult(pos);
    }

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (afterPosition == long.MaxValue)
        {

            yield break;
        }

        long next = afterPosition + 1;
        Dictionary<long, TEntry> pending = [];

        while (true)
        {
            TaskCompletionSource<bool> waiter = Volatile.Read(ref _signal);
            (long pos, TEntry entry)[] snapshot = [.. _entries];

            foreach ((long pos, TEntry entry) in snapshot)
            {
                if (pos >= next && !pending.ContainsKey(pos))
                {
                    pending[pos] = entry;
                }
            }

            bool progressed = false;
            while (pending.TryGetValue(next, out TEntry? entry))
            {
                ct.ThrowIfCancellationRequested();
                yield return (next, entry);
                pending.Remove(next);
                next++;
                progressed = true;
            }

            if (progressed)
            {
                continue;
            }

            await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, [EnumeratorCancellation] CancellationToken ct = default)
    {
        (long pos, TEntry entry)[] snapshot = [.. _entries];
        Array.Sort(snapshot, (a, b) => b.pos.CompareTo(a.pos));
        await Task.Yield();
        foreach ((long pos, TEntry entry) in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (pos < beforePosition)
            {
                yield return (pos, entry);
            }
        }
    }

    public async Task<(long journalPosition, TEntry entry)> WaitForAsync(long afterPosition, Func<TEntry, bool> match, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        while (true)
        {
            TaskCompletionSource<bool> waiter = Volatile.Read(ref _signal);
            (long pos, TEntry entry)[] snapshot = [.. _entries];

            (long pos, TEntry entry)? candidate = null;
            foreach ((long pos, TEntry e) in snapshot)
            {
                if (pos > afterPosition && match(e))
                {
                    if (candidate is null || pos < candidate.Value.pos)
                    {
                        candidate = (pos, e);
                    }
                }
            }

            if (candidate is not null)
            {
                ct.ThrowIfCancellationRequested();
                return (candidate.Value.pos, candidate.Value.entry);
            }

            await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken ct = default) where TDerived : TEntry
    {
        ArgumentNullException.ThrowIfNull(match);
        while (true)
        {
            TaskCompletionSource<bool> waiter = Volatile.Read(ref _signal);
            (long pos, TEntry entry)[] snapshot = [.. _entries];

            (long pos, TDerived entry)? candidate = null;
            foreach ((long pos, TEntry e) in snapshot)
            {
                if (pos > afterPosition && e is TDerived d && match(d))
                {
                    if (candidate is null || pos < candidate.Value.pos)
                    {
                        candidate = (pos, d);
                    }
                }
            }

            if (candidate is not null)
            {
                ct.ThrowIfCancellationRequested();
                return (candidate.Value.pos, candidate.Value.entry);
            }

            await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
