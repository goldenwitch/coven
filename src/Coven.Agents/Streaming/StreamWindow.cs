// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Agents.Streaming;

public readonly record struct StreamWindow(
    IEnumerable<string> PendingChunks,
    int ChunkCount,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEmitAt
);

