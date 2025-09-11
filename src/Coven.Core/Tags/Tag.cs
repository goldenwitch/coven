// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Tags;

public static class Tag
{
    private static readonly AsyncLocal<ITagScope?> currentScope = new();
    private static readonly AsyncLocal<SelectionFence?> currentFence = new();

    // Accessor to the current scope's set (throws if no scope)
    public static ISet<string> Current
    {
        get
        {
            var scope = currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
            return scope.Set;
        }
    }

    public static void Add(string tag)
    {
        var scope = currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
        scope.Add(tag);
    }

    public static bool Contains(string tag)
    {
        var scope = currentScope.Value ?? throw new InvalidOperationException("No active tag scope.");
        return scope.Contains(tag);
    }

    public static ITagScope? BeginScope(ITagScope scope)
    {
        var prev = currentScope.Value;
        currentScope.Value = scope;
        return prev;
    }

    public static void EndScope(ITagScope? previous)
    {
        currentScope.Value = previous;
    }

    internal static ITagScope NewScope(IEnumerable<string>? tags)
    {
        return new BoardTagScope(tags);
    }

    // Internals used by the router to manage per-step tag epochs without mutating tags.
    internal static void IncrementEpoch()
    {
        if (currentScope.Value is BoardTagScope b) b.IncrementEpoch();
    }

    internal static IReadOnlyList<string> CurrentEpochTags()
    {
        if (currentScope.Value is BoardTagScope b) return b.GetCurrentEpochTags();
        return Array.Empty<string>();
    }

    internal static void Log(string message)
    {
        if (currentScope.Value is BoardTagScope b) b.AddLog(message);
    }

    internal static IReadOnlyList<string> GetLogs()
    {
        if (currentScope.Value is BoardTagScope b) return b.GetLogs();
        return Array.Empty<string>();
    }

    // Selection fence: constrain the next selection to a set of allowed block instances.
    private sealed class SelectionFence
    {
        public required HashSet<object> Allowed { get; init; }
        public required int Epoch { get; init; }
    }

    internal static void SetNextSelectionFence(IEnumerable<object> allowed)
    {
        if (currentScope.Value is not BoardTagScope b) return;
        currentFence.Value = new SelectionFence
        {
            Allowed = new HashSet<object>(allowed),
            Epoch = b.Epoch
        };
    }

    internal static IReadOnlyCollection<object>? GetFenceForCurrentEpoch()
    {
        if (currentScope.Value is not BoardTagScope b) return null;
        var f = currentFence.Value;
        if (f is null) return null;
        if (f.Epoch != b.Epoch) return null;
        return f.Allowed;
    }
}