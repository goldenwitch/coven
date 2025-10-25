// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Result of batch-transmuting a set of chunks into a single output,
/// with an optional remainder chunk representing the unused tail of the
/// final input chunk.
/// </summary>
/// <typeparam name="TChunk">The input chunk type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public readonly record struct BatchTransmuteResult<TChunk, TOutput>(
    TOutput Output,
    bool HasRemainder,
    TChunk? Remainder
);

