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

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> TailAsync(long afterPosition = 0, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (afterPosition == long.MaxValue)
        {
            yield break;
        }

        long nextPosition = afterPosition + 1;

        while (true)
        {
            ChannelReader<bool> reader = _writeSignal.Reader;
            // Reset level (best-effort) so subsequent WaitToReadAsync will truly wait if no new writes arrive.
            reader.TryRead(out _);

            bool progressed = false;
            while (true)
            {
                (long position, TEntry entry) item;
                lock (_lock)
                {
                    int index = (int)(nextPosition - 1);
                    if (index >= 0 && index < _entries.Count)
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
                continue;
            }

            await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<(long journalPosition, TEntry entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        (long position, TEntry entry)[] snapshot;
        lock (_lock)
        {
            snapshot = [.. _entries];
        }

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

        long scanPos = afterPosition + 1;

        while (true)
        {
            ChannelReader<bool> reader = _writeSignal.Reader;
            // Reset level (best-effort) so subsequent WaitToReadAsync will truly wait if no new writes arrive.
            reader.TryRead(out _);

            int count;
            lock (_lock)
            {
                count = _entries.Count;
            }

            for (long positionIndex = scanPos; positionIndex <= count; positionIndex++)
            {
                (long position, TEntry entry) item;
                lock (_lock)
                {
                    item = _entries[(int)(positionIndex - 1)];
                }

                if (match(item.entry))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return (item.position, item.entry);
                }
            }

            scanPos = count + 1;
            await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
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
        (long position, TEntry entry) = await WaitForAsync(afterPosition, e => e is TDerived d && match(d), cancellationToken).ConfigureAwait(false);
        return (position, (TDerived)entry);
    }
}
