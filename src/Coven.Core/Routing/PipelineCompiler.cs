// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Di;
using Coven.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Coven.Core.Routing;

internal sealed class PipelineCompiler
{
    private readonly IReadOnlyList<RegisteredBlock> _registry;
    private readonly ISelectionStrategy _selector;
    private readonly SelectionEngine _engine;

    internal PipelineCompiler(IReadOnlyList<RegisteredBlock> registry, ISelectionStrategy? selector = null)
    {
        _registry = registry;
        _selector = selector ?? new DefaultSelectionStrategy();
        _engine = new SelectionEngine(registry, _selector);
    }

    internal Func<TIn, CancellationToken, Task<TOut>> Compile<TIn, TOut>(Type startType, Type targetType)
    {
        RegisteredBlock[] candidates = [.. _registry];

        return async (input, cancellationToken) =>
        {
            object current = input!;

            if (candidates.Length == 0)
            {
                return targetType.IsInstanceOfType(current)
                    ? (TOut)current
                    : throw new InvalidOperationException($"No next step available from type {current.GetType().Name} to reach {targetType.Name}.");
            }

            int lastIndex = -1;
            int maxSteps = candidates.Length;

            Dictionary<int, object> cache = [];
            IServiceProvider? sp = CovenExecutionScope.CurrentProvider;
            ILogger? logger = null;
            string ritualId = Guid.NewGuid().ToString("N");
            try
            {
                ILoggerFactory? lf = sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                logger = lf?.CreateLogger("Coven.Ritual");
            }
            catch { /* ignore if logging not configured */ }
            IDisposable? ritualScope = null;
            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                ritualScope = logger.BeginScope("ritual:" + ritualId);
                CoreLog.RitualBegin(logger, ritualId, startType.Name, targetType.Name);
            }
            try
            {
                for (int step = 0; step < maxSteps; step++)
                {
                    if (current is null)
                    {
                        if (logger is not null && logger.IsEnabled(LogLevel.Error))
                        {
                            CoreLog.RitualAbortedNull(logger, ritualId, step);
                        }
                        throw new InvalidOperationException("Pipeline received a null value; cannot select next step.");
                    }
                    IReadOnlyCollection<object>? fence = Tag.GetFenceForCurrentEpoch();
                    Tag.Log($"building forward; fence={fence?.Count ?? 0} epochTags=[{string.Join(',', Tag.CurrentEpochTags())}]");
                    RegisteredBlock? chosen = _engine.SelectNext(current, fence, lastIndex, forwardOnly: true);

                    if (chosen is null)
                    {
                        if (targetType.IsInstanceOfType(current))
                        {
                            if (logger is not null && logger.IsEnabled(LogLevel.Information))
                            {
                                CoreLog.RitualEndSatisfied(logger, ritualId, targetType.Name, step);
                            }
                            return (TOut)current;
                        }
                        if (logger is not null && logger.IsEnabled(LogLevel.Warning))
                        {
                            CoreLog.RitualBlocked(logger, ritualId, current.GetType().Name, lastIndex, targetType.Name);
                        }
                        throw new InvalidOperationException($"No next step available from type {current.GetType().Name} after index {lastIndex} to reach {targetType.Name}.");
                    }
                    Tag.Log($"selected {chosen.BlockTypeName} at idx={chosen.RegistryIndex}");
                    if (logger is not null && logger.IsEnabled(LogLevel.Information))
                    {
                        CoreLog.RitualSelect(logger, chosen.BlockTypeName, chosen.RegistryIndex, current.GetType().Name, ritualId);
                    }

                    // Bump epoch so tags added by this block apply to the next selection.
                    Tag.IncrementEpoch();
                    object instance = chosen.Activator.GetInstance(sp, cache, chosen);
                    try
                    {
                        if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        {
                            CoreLog.RitualInvoke(logger, chosen.BlockTypeName, ritualId);
                        }
                        current = await chosen.Invoke(instance, current, cancellationToken).ConfigureAwait(false);
                        if (logger is not null && logger.IsEnabled(LogLevel.Information))
                        {
                            CoreLog.RitualComplete(logger, chosen.BlockTypeName, current?.GetType().Name ?? "null", ritualId);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (logger is not null && logger.IsEnabled(LogLevel.Error))
                        {
                            CoreLog.RitualBlockFailed(logger, chosen.BlockTypeName, ritualId, ex);
                        }
                        throw;
                    }
                    Tag.Add($"by:{chosen.BlockTypeName}");
                    // Emit forward-hint tags for downstream candidates of this block to bias next selection
                    if (chosen.ForwardNextTags is { Count: > 0 })
                    {
                        foreach (string t in chosen.ForwardNextTags)
                        {
                            Tag.Add(t);
                        }

                    }
                    lastIndex = chosen.RegistryIndex;
                }

                // After exhausting forward-compatible steps (or step cap), validate final assignability.
                if (targetType.IsInstanceOfType(current))
                {
                    return (TOut)current;
                }


                if (logger is not null && logger.IsEnabled(LogLevel.Warning))
                {
                    CoreLog.RitualExhausted(logger, targetType.Name, ritualId);
                }
                throw new InvalidOperationException($"Reached step limit without producing {targetType.Name}.");
            }
            finally
            {
                ritualScope?.Dispose();
            }
        };
    }

    internal bool PathExists(Type start, Type target)
    {
        if (target.IsAssignableFrom(start))
        {
            return true;
        }


        HashSet<Type> visited = [];
        Queue<Type> q = new();
        _ = visited.Add(start);
        q.Enqueue(start);
        while (q.Count > 0)
        {
            Type cur = q.Dequeue();
            if (target.IsAssignableFrom(cur))
            {
                return true;
            }


            foreach (RegisteredBlock e in _registry)
            {
                if (e.InputType.IsAssignableFrom(cur))
                {
                    if (visited.Add(e.OutputType))
                    {
                        q.Enqueue(e.OutputType);
                    }

                }
            }
        }
        return false;
    }
}
