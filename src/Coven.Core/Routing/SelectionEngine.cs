// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tricks;

namespace Coven.Core.Routing;

// Small helper to centralize candidate filtering + strategy selection for a single step.
internal sealed class SelectionEngine
{
    private readonly IReadOnlyList<RegisteredBlock> registry;
    private readonly ISelectionStrategy strategy;

    internal SelectionEngine(IReadOnlyList<RegisteredBlock> registry, ISelectionStrategy strategy)
    {
        this.registry = registry;
        this.strategy = strategy;
    }

    internal RegisteredBlock? SelectNext(object currentValue, IReadOnlyCollection<object>? fence, int lastIndex, bool forwardOnly)
    {
        var forward = new List<RegisteredBlock>();
        for (int i = 0; i < registry.Count; i++)
        {
            var c = registry[i];
            if (forwardOnly && c.RegistryIndex <= lastIndex) continue;
            if (!c.InputType.IsInstanceOfType(currentValue)) continue;
            if (fence is not null && !fence.Contains(c.Descriptor.BlockInstance)) continue;
            forward.Add(c);
        }

        if (forward.Count == 0) return null;

        // Project to public-facing candidates for the strategy
        var projected = new List<SelectionCandidate>(forward.Count);
        for (int i = 0; i < forward.Count; i++)
        {
            var rb = forward[i];
            bool isTrick = rb.Descriptor.BlockInstance is IMagikTrick;
            projected.Add(new SelectionCandidate(
                rb.RegistryIndex,
                rb.InputType,
                rb.OutputType,
                rb.BlockTypeName,
                rb.Capabilities is IReadOnlyCollection<string> rc ? rc : new List<string>(rb.Capabilities),
                isTrick
            ));
        }
        var chosen = strategy.SelectNext(projected);
        // Map back by registry index
        for (int i = 0; i < forward.Count; i++)
        {
            if (forward[i].RegistryIndex == chosen.RegistryIndex) return forward[i];
        }
        throw new InvalidOperationException("SelectionStrategy returned an unknown candidate (registry index not in forward set).");
    }
}