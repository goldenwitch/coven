using System.Collections.Generic;

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
        return strategy.SelectNext(forward);
    }
}

