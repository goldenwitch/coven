// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Flush predicate that triggers when the in-memory snapshot reaches a minimum entry count.
/// Useful as a simple batching policy for throughput.
/// </summary>
/// <typeparam name="TEntry">The journal entry type.</typeparam>
public sealed class CountThresholdFlushPredicate<TEntry> : IFlushPredicate<TEntry>
{
    private readonly int _threshold;

    /// <summary>
    /// Creates a predicate with the specified minimum snapshot size.
    /// </summary>
    /// <param name="threshold">Minimum number of entries required to flush. Must be &gt;= 1.</param>
    public CountThresholdFlushPredicate(int threshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 1);
        _threshold = threshold;
    }

    /// <summary>
    /// Returns true when <paramref name="snapshot"/> meets or exceeds the configured threshold.
    /// </summary>
    /// <param name="snapshot">The current in-memory snapshot buffer.</param>
    public bool ShouldFlush(IReadOnlyList<(long position, TEntry entry)> snapshot)
        => snapshot.Count >= _threshold;
}
