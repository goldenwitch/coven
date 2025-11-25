// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Determines when the in-memory snapshot should be flushed to persistent storage.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public interface IFlushPredicate<TEntry>
{
    /// <summary>
    /// Returns true when the provided snapshot meets the flush criteria.
    /// </summary>
    /// <param name="snapshot">Current in-memory snapshot buffer.</param>
    bool ShouldFlush(IReadOnlyList<(long position, TEntry entry)> snapshot);
}
