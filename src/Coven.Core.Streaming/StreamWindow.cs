// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

public readonly record struct StreamWindow<TChunk>(
    IEnumerable<TChunk> PendingChunks,
    int ChunkCount,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEmitAt
);

