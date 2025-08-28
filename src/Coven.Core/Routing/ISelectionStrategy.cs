namespace Coven.Core.Routing;

// Public strategy interface to allow callers to override routing behavior
// without exposing internal block types. Implementations should be deterministic
// to ensure predictable pipelines.
public interface ISelectionStrategy
{
    // Given forward-compatible candidates, return the chosen one.
    // Throw if none are available.
    SelectionCandidate SelectNext(IReadOnlyList<SelectionCandidate> forward);
}
