// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Snapshot of the current streaming window passed to window policies.
/// </summary>
/// <typeparam name="TChunk">Chunk type under consideration.</typeparam>
public readonly record struct StreamWindow<TChunk>(
/// <summary>Recent chunks in the window (respecting MinChunkLookback semantics).</summary>
IEnumerable<TChunk> PendingChunks,
/// <summary>Total number of chunks observed in the current buffer.</summary>
int ChunkCount,
/// <summary>Timestamp when windowing started.</summary>
DateTimeOffset StartedAt,
/// <summary>Timestamp of the last emit.</summary>
DateTimeOffset LastEmitAt
);
