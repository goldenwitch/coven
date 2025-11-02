// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Snapshot of the current streaming window passed to window policies.
/// </summary>
/// <typeparam name="TChunk">Chunk type under consideration.</typeparam>
/// <param name="PendingChunks">Recent chunks in the window (respecting MinChunkLookback semantics).</param>
/// <param name="ChunkCount">Total number of chunks observed in the current buffer.</param>
/// <param name="StartedAt">Timestamp when windowing started.</param>
/// <param name="LastEmitAt">Timestamp of the last emit.</param>
public readonly record struct StreamWindow<TChunk>(
    IEnumerable<TChunk> PendingChunks,
    int ChunkCount,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEmitAt
);
