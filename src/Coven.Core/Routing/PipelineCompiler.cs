using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coven.Core.Tags;

namespace Coven.Core.Routing;

internal sealed class PipelineCompiler
{
    private readonly IReadOnlyList<RegisteredBlock> registry;
    private readonly ISelectionStrategy selector;

    internal PipelineCompiler(IReadOnlyList<RegisteredBlock> registry, ISelectionStrategy? selector = null)
    {
        this.registry = registry;
        this.selector = selector ?? new DefaultSelectionStrategy();
    }

    internal Func<TIn, Task<TOut>> Compile<TIn, TOut>(Type startType, Type targetType)
    {
        var candidates = registry.ToArray();

        return async (TIn input) =>
        {
            object current = input!;

            if (candidates.Length == 0)
            {
                if (targetType.IsInstanceOfType(current)) return (TOut)current;
                throw new InvalidOperationException($"No next step available from type {current.GetType().Name} to reach {targetType.Name}.");
            }

            int lastIndex = -1;
            int maxSteps = candidates.Length;

            for (int step = 0; step < maxSteps; step++)
            {
                var forward = new List<RegisteredBlock>();
                for (int i = 0; i < candidates.Length; i++)
                {
                    var c = candidates[i];
                    if (c.RegistryIndex <= lastIndex) continue;
                    if (!c.InputType.IsInstanceOfType(current)) continue;
                    forward.Add(c);
                }

                if (forward.Count == 0)
                {
                    if (targetType.IsInstanceOfType(current))
                    {
                        return (TOut)current;
                    }
                    throw new InvalidOperationException($"No next step available from type {current.GetType().Name} after index {lastIndex} to reach {targetType.Name}.");
                }

                var chosen = selector.SelectNext(forward);

                current = await chosen.Invoke(current).ConfigureAwait(false);
                Tag.Add($"by:{chosen.BlockTypeName}");
                lastIndex = chosen.RegistryIndex;

                if (targetType.IsInstanceOfType(current))
                {
                    return (TOut)current;
                }
            }

            if (targetType.IsInstanceOfType(current)) return (TOut)current;
            throw new InvalidOperationException($"Reached step limit without producing {targetType.Name}.");
        };
    }

    internal bool PathExists(Type start, Type target)
    {
        if (target.IsAssignableFrom(start)) return true;
        var visited = new HashSet<Type>();
        var q = new Queue<Type>();
        visited.Add(start);
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (target.IsAssignableFrom(cur)) return true;
            foreach (var e in registry)
            {
                if (e.InputType.IsAssignableFrom(cur))
                {
                    if (visited.Add(e.OutputType)) q.Enqueue(e.OutputType);
                }
            }
        }
        return false;
    }
}

