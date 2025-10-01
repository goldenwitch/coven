// SPDX-License-Identifier: BUSL-1.1
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Coven.Core;

/// <summary>
/// In-memory implementation of IScrivener<T> journal with simple, single-process semantics;
/// supports tailing, backward read, and predicate waits.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public sealed class InMemoryScrivener<TEntry> : IScrivener<TEntry> where TEntry : notnull
{
    // Using System.Threading.Lock aligns with repo guidance when a lock is warranted; Dotnet's async sugar is a typing nightmare for a task generator like this.
    private readonly Lock _lock = new();
    private readonly List<(long position, TEntry entry)> _entries = [];

    private long _nextPosition; // first assigned will be 1

    // Level-triggered readiness via bounded Channel (capacity=1) to coalesce multiple writes.
    private readonly Channel<bool> _writeSignal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    /// <summary>
    /// Appends an entry to the journal and returns its assigned position.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The monotonically increasing journal position.</returns>
    public Task<long> WriteAsync(TEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Scriveners MUST support a long position.
        // This in-memory implementation relies on int-indexed storage for simplicity; guard against overflow.
        if (_entries.Count == int.MaxValue)
        {
            throw new InvalidOperationException("InMemoryScrivener capacity exceeded: underlying storage is int-indexed. Scriveners MUST support a long position.");
        }

        long position;
        lock (_lock)
        {
            position = ++_nextPosition; // assign and append atomically under the lock
            _entries.Add((position, entry));
        }

        // Notify waiters: write a token; multiple writes coalesce while buffer is full.
        _writeSignal.Writer.TryWrite(true);

        return Task.FromResult(position);
    }

    /// <summary>
    /// Streams entries forward with positions strictly greater than <paramref name="afterPosition"/>.
    /// </summary>
    /// <param name="afterPosition">Only entries after this position are returned.</param>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>An async sequence of (journalPosition, entry) pairs.</returns>
    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (afterPosition == long.MaxValue)
        {
            yield break;
        }

        long nextPosition = afterPosition + 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChannelReader<bool> reader = _writeSignal.Reader;
            // Reset level (best-effort) so subsequent WaitToReadAsync will truly wait if no new writes arrive.
            reader.TryRead(out _);

            bool progressed = false;
            while (true)
            {
                (long position, TEntry entry) item;
                lock (_lock)
                {
                    int index = IndexFromPosition(nextPosition);
                    if (index < _entries.Count)
                    {
                        item = _entries[index];
                    }
                    else
                    {
                        break;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                yield return (item.position, item.entry);
                nextPosition = item.position + 1;
                progressed = true;
            }

            if (progressed)
            {
                // Friendly yield after draining a backlog to avoid monopolizing the thread.
                await Task.Yield();
                continue;
            }

            await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Streams entries backward with positions strictly less than <paramref name="beforePosition"/>.
    /// </summary>
    /// <param name="beforePosition">Upper bound (exclusive); defaults to logical end.</param>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>An async sequence of (journalPosition, entry) pairs in descending order.</returns>
    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        (long position, TEntry entry)[] snapshot;
        lock (_lock)
        {
            snapshot = [.. _entries];
        }

        // Yield cooperatively to avoid CS1998 analyzer warnings and ensure fairness before potentially long loops.
        await Task.Yield();

        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (long position, TEntry entry) = snapshot[i];
            if (position < beforePosition)
            {
                yield return (position, entry);
            }
        }
    }

    /// <summary>
    /// Waits for the first journal entry at or after <paramref name="afterPosition"/> that matches <paramref name="match"/>.
    /// </summary>
    /// <param name="afterPosition">The last observed position; search starts at <c>afterPosition + 1</c>.</param>
    /// <param name="match">Predicate applied to each entry; the first match completes the wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <remarks>
    /// Behavior: If no matching entry exists yet, this method waits for future writes. It scans existing entries
    /// from the current scan position up to the latest count, then blocks on an internal signal that is triggered by writes.
    /// This provides level-triggered readiness and may coalesce multiple writes.
    /// </remarks>
    public async Task<(long journalPosition, TEntry entry)> WaitForAsync(long afterPosition, Func<TEntry, bool> match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (afterPosition == long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(afterPosition), "No further positions exist after long.MaxValue.");
        }

        await foreach ((long journalPosition, TEntry? entry) in TailAsync(afterPosition, cancellationToken))
        {
            if (match(entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return (journalPosition, entry);
            }
        }

        throw new InvalidOperationException("Unexpected end of tail enumeration.");
    }

    /// <summary>
    /// Waits for the first journal entry of type <typeparamref name="TDerived"/> at or after <paramref name="afterPosition"/>
    /// that matches <paramref name="match"/>.
    /// </summary>
    /// <param name="afterPosition">The last observed position; search starts at <c>afterPosition + 1</c>.</param>
    /// <param name="match">Predicate applied to entries of type <typeparamref name="TDerived"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <remarks>
    /// Behavior: If no matching entry exists yet, this method waits for future writes. It scans existing entries
    /// from the current scan position up to the latest count, then blocks on an internal signal that is triggered by writes.
    /// </remarks>
    public async Task<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, Func<TDerived, bool> match, CancellationToken cancellationToken = default) where TDerived : TEntry
    {
        ArgumentNullException.ThrowIfNull(match);
        (long journalPosition, TEntry entry) = await WaitForAsync(afterPosition, e => e is TDerived d && match(d), cancellationToken).ConfigureAwait(false);
        return (journalPosition, (TDerived)entry);
    }

    private static int IndexFromPosition(long position)
    {
        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be >= 1.");
        }

        long zeroBased = position - 1;
        if (zeroBased > int.MaxValue)
        {
            // Storage addressing in this in-memory implementation uses int-indexed list.
            throw new InvalidOperationException("Position exceeds in-memory addressing capacity. Scriveners MUST support a long position.");
        }

        return (int)zeroBased;
    }
}
