// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Transmutation;

/// <summary>
/// Specialization for batch transmutation from a sequence of chunks to a single output plus optional remainder.
/// </summary>
/// <typeparam name="TChunk">Input chunk type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public interface IBatchTransmuter<TChunk, TOutput> : ITransmuter<IEnumerable<TChunk>, BatchTransmuteResult<TChunk, TOutput>>
{
}

