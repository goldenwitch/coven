// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Scrivener;

/// <summary>
/// Detects recursive activation of IScrivener&lt;TEntry&gt; for the same TEntry during factory build.
/// Throws with actionable guidance to prevent stack overflows and ambiguous DI graphs.
/// </summary>
internal static class ScrivenerBuildReentrancy
{
    private sealed record Context(Type EntryType, bool HasTapper);
    private static readonly AsyncLocal<Stack<Context>?> _active = new();

    /// <summary>
    /// Marks the given entry type as "building" and throws if already in progress.
    /// </summary>
    /// <param name="entryType">The generic entry type for the scrivener being built.</param>
    /// <param name="innerType">The inner scrivener concrete type.</param>
    /// <param name="tapperType">The optional tapped scrivener type.</param>
    /// <returns>A disposable token that clears the in-progress marker on dispose.</returns>
    public static IDisposable Enter(Type entryType, Type innerType, Type? tapperType)
    {
        Stack<Context> stack = _active.Value ??= new Stack<Context>();
        Context current = new(entryType, tapperType is not null);

        if (stack.Count > 0)
        {
            Context parent = stack.Peek();
            if (parent.EntryType == entryType)
            {
                // Allow exactly one nested build for the same entry type when the parent is building a tapper
                // and the child is building a raw (non‑tapped) inner (e.g., a keyed inner for gateways).
                bool allowedNestedInner = parent.HasTapper && !current.HasTapper;
                if (!allowedNestedInner)
                {
                    string tap = tapperType?.Name ?? "none";
                    throw new InvalidOperationException(
                        $"Recursive resolution of IScrivener<{entryType.Name}> detected while building inner={innerType.Name}, tapper={tap}. " +
                        "Allowed pattern: during a tapper build, resolve a raw keyed inner scrivener. " +
                        "Avoid requesting another tapped/default IScrivener<T> inside a tapper.");
                }
            }
        }

        stack.Push(current);
        return new Popper(stack);
    }

    private sealed class Popper(Stack<Context> stack) : IDisposable
    {
        private readonly Stack<Context> _stack = stack;


        public void Dispose() => _stack.Pop();
    }
}
