// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Destination for flushed snapshots emitted by the flusher consumer.
/// Implementations should persist entries in order and atomically per snapshot.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public interface IFlushSink<TEntry>
{
    /// <summary>
    /// Persist the ordered snapshot entries.
    /// </summary>
    /// <param name="snapshot">Ordered list of (position, entry) pairs.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task AppendSnapshotAsync(IReadOnlyList<(long position, TEntry entry)> snapshot, CancellationToken cancellationToken = default);
}
