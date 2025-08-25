using System.Collections.Generic;
using System.Threading;

namespace Coven.Core.Tags;

public static class Tag
{
    private static readonly AsyncLocal<ITagScope?> currentScope = new();

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
}
