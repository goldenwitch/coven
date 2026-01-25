// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Transmutation;

/// <summary>
/// Result of batch-transmuting a set of chunks into a single output,
/// with an optional remainder chunk representing the unused tail of the
/// final input chunk.
/// </summary>
/// <typeparam name="TChunk">The input chunk type.</typeparam>
/// <typeparam name="TOutput">The output type. Must be non-null; transmuters are pure transforms.</typeparam>
public readonly record struct BatchTransmuteResult<TChunk, TOutput>(
    TOutput Output,
    bool HasRemainder,
    TChunk? Remainder
) where TOutput : notnull;

