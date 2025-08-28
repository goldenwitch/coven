using Coven.Core.Tags;
using Coven.Core.Di;

namespace Coven.Core.Routing;

internal sealed class PipelineCompiler
{
    private readonly IReadOnlyList<RegisteredBlock> registry;
    private readonly ISelectionStrategy selector;
    private readonly SelectionEngine engine;

    internal PipelineCompiler(IReadOnlyList<RegisteredBlock> registry, ISelectionStrategy? selector = null)
    {
        this.registry = registry;
        this.selector = selector ?? new DefaultSelectionStrategy();
        this.engine = new SelectionEngine(registry, this.selector);
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

            var cache = new Dictionary<int, object>();
            var sp = CovenExecutionScope.CurrentProvider;
            for (int step = 0; step < maxSteps; step++)
            {
                var fence = Tag.GetFenceForCurrentEpoch();
                Tag.Log($"building forward; fence={(fence?.Count ?? 0)} epochTags=[{string.Join(',', Tag.CurrentEpochTags())}]");
                var chosen = engine.SelectNext(current, fence, lastIndex, forwardOnly: true);

                if (chosen is null)
                {
                    if (targetType.IsInstanceOfType(current))
                    {
                        return (TOut)current;
                    }
                    throw new InvalidOperationException($"No next step available from type {current.GetType().Name} after index {lastIndex} to reach {targetType.Name}.");
                }
                Tag.Log($"selected {chosen.BlockTypeName} at idx={chosen.RegistryIndex}");

                // Bump epoch so tags added by this block apply to the next selection.
                Tag.IncrementEpoch();
                var instance = chosen.Activator.GetInstance(sp, cache, chosen);
                current = await chosen.Invoke(instance, current).ConfigureAwait(false);
                Tag.Add($"by:{chosen.BlockTypeName}");
                // Emit forward-hint tags for downstream candidates of this block to bias next selection
                if (chosen.ForwardNextTags is { Count: > 0 })
                {
                    foreach (var t in chosen.ForwardNextTags) Tag.Add(t);
                }
                lastIndex = chosen.RegistryIndex;
            }

            // After exhausting forward-compatible steps (or step cap), validate final assignability.
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
