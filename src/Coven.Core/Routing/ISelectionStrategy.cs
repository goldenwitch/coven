// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Routing;

/// <summary>
/// Strategy for choosing the next block in a pipeline given a set of forward-compatible candidates.
/// Implementations should be deterministic to ensure predictable pipelines.
/// </summary>
public interface ISelectionStrategy
{
    /// <summary>
    /// Selects the next candidate from a forward-compatible set.
    /// </summary>
    /// <param name="forward">Forward-compatible candidates.</param>
    /// <returns>The chosen candidate.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no selection is possible.</exception>
    SelectionCandidate SelectNext(IReadOnlyList<SelectionCandidate> forward);
}
