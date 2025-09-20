// SPDX-License-Identifier: BUSL-1.1


namespace Coven.Core.Routing;

// Small helper to centralize candidate filtering + strategy selection for a single step.
internal sealed class SelectionEngine
{
    private readonly IReadOnlyList<RegisteredBlock> _registry;
    private readonly ISelectionStrategy _strategy;

    internal SelectionEngine(IReadOnlyList<RegisteredBlock> registry, ISelectionStrategy strategy)
    {
        _registry = registry;
        _strategy = strategy;
    }

    internal RegisteredBlock? SelectNext(object currentValue, IReadOnlyCollection<object>? fence, int lastIndex, bool forwardOnly)
    {
        List<RegisteredBlock> forward = [];
        for (int i = 0; i < _registry.Count; i++)
        {
            RegisteredBlock c = _registry[i];
            if (forwardOnly && c.RegistryIndex <= lastIndex)
            {
                continue;
            }


            if (!c.InputType.IsInstanceOfType(currentValue))
            {
                continue;
            }


            if (fence is not null && !fence.Contains(c.Descriptor.BlockInstance))
            {
                continue;
            }


            forward.Add(c);
        }

        if (forward.Count == 0)
        {
            return null;
        }

        // Project to public-facing candidates for the strategy

        List<SelectionCandidate> projected = new(forward.Count);
        for (int i = 0; i < forward.Count; i++)
        {
            RegisteredBlock rb = forward[i];
            projected.Add(new SelectionCandidate(
                rb.RegistryIndex,
                rb.InputType,
                rb.OutputType,
                rb.BlockTypeName,
                rb.Capabilities is IReadOnlyCollection<string> rc ? rc : [.. rb.Capabilities]
            ));
        }
        SelectionCandidate chosen = _strategy.SelectNext(projected);
        // Map back by registry index
        for (int i = 0; i < forward.Count; i++)
        {
            if (forward[i].RegistryIndex == chosen.RegistryIndex)
            {
                return forward[i];
            }

        }
        throw new InvalidOperationException("SelectionStrategy returned an unknown candidate (registry index not in forward set).");
    }
}