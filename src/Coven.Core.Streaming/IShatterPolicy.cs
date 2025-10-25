// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Defines a policy that scatters a source entry into one or more chunks.
/// </summary>
/// <typeparam name="TSource">The source entry type to scatter.</typeparam>
/// <typeparam name="TChunk">The chunk type produced.</typeparam>
public interface IShatterPolicy<TSource, TChunk>
{
    /// <summary>
    /// Produces zero or more chunks from a single source entry.
    /// Implementations should avoid side-effects and honor cancellation in callers.
    /// </summary>
    /// <param name="source">The source entry to scatter.</param>
    /// <returns>Chunks yielded from the provided <paramref name="source"/>.</returns>
    IEnumerable<TChunk> Shatter(TSource source);
}

