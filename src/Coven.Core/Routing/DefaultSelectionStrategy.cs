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

        // 3) Capability overlap; tie-break by registration order (smallest index wins)
        int bestScore = int.MinValue;
        int bestIdx = int.MaxValue;
        RegisteredBlock? chosen = null;
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            int score = 0;
            if (c.Capabilities.Count > 0)
            {
                foreach (var t in Tag.Current)
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

