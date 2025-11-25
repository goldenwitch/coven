// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Configuration for the file-backed scrivener and flusher daemon.
/// </summary>
public sealed class FileScrivenerConfig
{
    /// <summary>
    /// Destination file path for appended snapshots. Directories are created if necessary.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Default threshold for snapshot size when no predicate is supplied.
    /// </summary>
    public int FlushThreshold { get; init; } = 100;

    /// <summary>
    /// Capacity of the ring buffer (bounded channel) for flushing snapshots.
    /// </summary>
    public int FlushQueueCapacity { get; init; } = 8;
}
