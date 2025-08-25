using System;
using System.Collections.Generic;
using Coven.Core.Tags;

namespace Coven.Core.Routing;

internal sealed class DefaultSelectionStrategy : ISelectionStrategy
{
    public RegisteredBlock SelectNext(IReadOnlyList<RegisteredBlock> forward)
    {
        if (forward.Count == 0)
            throw new InvalidOperationException("No forward candidates to select from.");

        var epochTags = Tag.CurrentEpochTags();

        // 1) Explicit index override: to:#<index>
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            if (Tag.Contains($"to:#{c.RegistryIndex}")) return c;
        }

        // 2) Explicit type name override: to:<BlockTypeName>
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            if (Tag.Contains($"to:{c.BlockTypeName}")) return c;
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
        int bestScore = int.MinValue;
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
            int score = 0;
            if (c.Capabilities.Count > 0)
            {
                foreach (var t in effectiveTags)
                {
                    if (c.Capabilities.Contains(t)) score++;
                }
            }
            if (score > bestScore || (score == bestScore && c.RegistryIndex < bestIdx))
            {
                bestScore = score;
                bestIdx = c.RegistryIndex;
                chosen = c;
            }
        }

        return chosen!;
    }
}
