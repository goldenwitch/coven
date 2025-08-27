using Coven.Core.Tags;

namespace Coven.Core.Routing;

internal sealed class DefaultSelectionStrategy : ISelectionStrategy
{
    public RegisteredBlock SelectNext(IReadOnlyList<RegisteredBlock> forward)
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
                if (c.Descriptor.BlockInstance is Tricks.IMagikTrick) return c;
            }
        }

        // 4) Capability overlap using current-epoch tags plus persistent preferences (prefer:*);
        // tie-break by registration order (smallest index wins)
        // Best fit rules: type already filtered; choose by total capability matches across all tags,
        // including forward-preference tags (next:*), then break ties by registration order.
        int bestTotalScore = int.MinValue;
        int bestIdx = int.MaxValue;
        RegisteredBlock? chosen = null;
        // Union: epoch tags + persistent prefer:* from all-time tags
        var effectiveTags = new HashSet<string>(epochTags, StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tag.Current)
        {
            if (t.StartsWith("prefer:", StringComparison.OrdinalIgnoreCase)) effectiveTags.Add(t);
        }
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            int total = 0;
            if (c.Capabilities.Count > 0)
            {
                foreach (var t in effectiveTags)
                {
                    if (c.Capabilities.Contains(t)) total++;
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
