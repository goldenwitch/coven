using Coven.Core.Tags;
using Coven.Core.Tricks;

namespace Coven.Core.Routing;

internal sealed class DefaultSelectionStrategy : ISelectionStrategy
{
    public SelectionCandidate SelectNext(IReadOnlyList<SelectionCandidate> forward)
    {
        if (forward.Count == 0)
            throw new InvalidOperationException("No forward candidates to select from.");

        var epochTags = Tag.CurrentEpochTags();

        // 1) Explicit index override: to:#<index> (only consider tags from the current epoch)
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            if (System.Linq.Enumerable.Contains(epochTags, $"to:#{c.RegistryIndex}")) return c;
        }

        // 2) Explicit type name override: to:<BlockTypeName> (current-epoch only)
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            if (System.Linq.Enumerable.Contains(epochTags, $"to:{c.BlockTypeName}")) return c;
        }

        // 3) After the first hop (epoch has by:*), prefer Tricks as forks to run first
        bool afterFirstHop = false;
        foreach (var t in epochTags) { if (t.StartsWith("by:", StringComparison.OrdinalIgnoreCase)) { afterFirstHop = true; break; } }
        if (afterFirstHop)
        {
            for (int i = 0; i < forward.Count; i++)
            {
                var c = forward[i];
                if (c.IsTrick) return c;
            }
        }

        // 4) Capability overlap using current-epoch tags plus any persistent routing hints;
        // tie-break by registration order (smallest index wins)
        // Best fit rules: type already filtered; choose by total capability matches across all tags,
        // including forward-hint tags (next:*), then break ties by registration order.
        int bestTotalScore = int.MinValue;
        int bestIdx = int.MaxValue;
        SelectionCandidate? chosen = null;
        // Union: epoch tags
        var effectiveTags = new HashSet<string>(epochTags, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            int total = 0;
            if (c.Capabilities.Count > 0)
            {
                foreach (var t in effectiveTags)
                {
                    if (System.Linq.Enumerable.Contains(c.Capabilities, t)) total++;
                }
            }
            if (total > bestTotalScore || (total == bestTotalScore && c.RegistryIndex < bestIdx))
            {
                bestTotalScore = total;
                bestIdx = c.RegistryIndex;
                chosen = c;
            }
        }

        return chosen!;
    }
}
