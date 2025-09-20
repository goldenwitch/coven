// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;

namespace Coven.Core.Routing;

internal sealed class DefaultSelectionStrategy : ISelectionStrategy
{
    public SelectionCandidate SelectNext(IReadOnlyList<SelectionCandidate> forward)
    {
        if (forward.Count == 0)
            throw new InvalidOperationException("No forward candidates to select from.");

        var epochTags = Tag.CurrentEpochTags();

        // Capability overlap using current-epoch tags; tie-break by registration order (smallest index wins)
        // Best fit rules: type already filtered; choose by total capability matches across all tags,
        // then break ties by registration order.
        int bestTotalScore = int.MinValue;
        int bestIdx = int.MaxValue;
        SelectionCandidate? chosen = null;
        
        // Consider: epoch tags only (forward-next hints are applied outside selection via Board)
        var effectiveTags = new HashSet<string>(epochTags, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < forward.Count; i++)
        {
            var c = forward[i];
            int total = 0;
            if (c.Capabilities.Count > 0)
            {
                foreach (var t in effectiveTags)
                {
                    if (Enumerable.Contains(c.Capabilities, t)) total++;
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
