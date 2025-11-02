// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

/// <summary>
/// Static helpers for manipulating the ambient tag scope of a ritual.
/// </summary>
public static class Tag
{
    private static readonly AsyncLocal<ITagScope?> _currentScope = new();
    private static readonly AsyncLocal<SelectionFence?> _currentFence = new();

    /// <summary>
    /// Gets the current tag set for the active scope.
    /// </summary>
    public static ISet<string> Current
    {
        get
        {
            ITagScope scope = _currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
            return scope.TagSet;
        }
    }

    /// <summary>Adds a tag to the current scope.</summary>
    public static void Add(string tag)
    {
        ITagScope scope = _currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
        scope.Add(tag);
    }

    /// <summary>Returns true if the current scope contains the specified tag.</summary>
    public static bool Contains(string tag)
    {
        ITagScope scope = _currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
        return scope.Contains(tag);
    }

    /// <summary>
    /// Begins a new ambient tag scope and returns the previous scope to allow restoration.
    /// </summary>
    /// <param name="scope">The new scope to activate.</param>
    /// <returns>The previous scope value.</returns>
    public static ITagScope? BeginScope(ITagScope scope)
    {
        ITagScope? prev = _currentScope.Value;
        _currentScope.Value = scope;
        return prev;
    }

    /// <summary>Restores the ambient tag scope to a previous value.</summary>
    public static void EndScope(ITagScope? previous)
    {
        _currentScope.Value = previous;
    }

    internal static ITagScope NewScope(IEnumerable<string>? tags)
    {
        return new BoardTagScope(tags);
    }

    // Internals used by the router to manage per-step tag epochs without mutating tags.
    internal static void IncrementEpoch()
    {
        if (_currentScope.Value is BoardTagScope b)
        {
            b.IncrementEpoch();
        }

    }

    internal static IReadOnlyList<string> CurrentEpochTags()
    {
        return _currentScope.Value is BoardTagScope b ? b.GetCurrentEpochTags() : [];
    }

    internal static void Log(string message)
    {
        if (_currentScope.Value is BoardTagScope b)
        {
            b.AddLog(message);
        }

    }

    internal static IReadOnlyList<string> GetLogs()
    {
        return _currentScope.Value is BoardTagScope b ? b.GetLogs() : [];
    }

    // Selection fence: constrain the next selection to a set of allowed block instances.
    private sealed class SelectionFence
    {
        public required HashSet<object> Allowed { get; init; }
        public required int Epoch { get; init; }
    }

    internal static void SetNextSelectionFence(IEnumerable<object> allowed)
    {
        if (_currentScope.Value is not BoardTagScope b)
        {
            return;
        }


        _currentFence.Value = new SelectionFence
        {
            Allowed = [.. allowed],
            Epoch = b.Epoch
        };
    }

    internal static IReadOnlyCollection<object>? GetFenceForCurrentEpoch()
    {
        if (_currentScope.Value is not BoardTagScope b)
        {
            return null;
        }


        SelectionFence? f = _currentFence.Value;
        return f is null ? null : f.Epoch != b.Epoch ? null : (IReadOnlyCollection<object>)f.Allowed;
    }
}
