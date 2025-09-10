using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Coven.Core.Routing;
using Coven.Core.Tags;

namespace Coven.Core;

public class Board : IBoard
{
    internal enum BoardMode
    {
        Push,
        Pull
    }

    private readonly BoardMode currentMode = BoardMode.Push;
    internal IReadOnlyList<MagikBlockDescriptor> Registry { get; }
    private readonly IReadOnlyList<RegisteredBlock> registeredBlocks;
    private readonly PipelineCompiler compiler;
    private readonly ConcurrentDictionary<string, HashSet<string>> pullBranchTags = new(StringComparer.Ordinal);
    private readonly PullOptions? pullOptions;
    private readonly ISelectionStrategy? selectionStrategy;

    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry, PullOptions? pullOptions = null, ISelectionStrategy? selectionStrategy = null)
    {
        currentMode = mode;
        Registry = registry;
        registeredBlocks = BuildRegisteredBlocks(registry);
        this.selectionStrategy = selectionStrategy;
        compiler = new PipelineCompiler(registeredBlocks, selectionStrategy);
        this.pullOptions = pullOptions;
        // Always precompile pipelines regardless of mode for consistency
        PrecompileAllPipelines();
    }

    private readonly ConcurrentDictionary<(Type start, Type target), Delegate> pipelineCache = new();

    // Internal non-generic step for orchestrators; merges tags, selects, executes, persists tags, returns output.
    internal async Task GetWorkPullAsync(object input, string? branchId, IReadOnlyCollection<string>? extraTags, IOrchestratorSink sink)
    {
        if (currentMode != BoardMode.Pull)
            throw new NotSupportedException("GetWork is only available in pull mode.");

        var bid = branchId ?? string.Empty;
        var baseTags = pullBranchTags.GetOrAdd(bid, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var merged = new HashSet<string>(baseTags, StringComparer.OrdinalIgnoreCase);
        if (extraTags is not null)
        {
            foreach (var t in extraTags) merged.Add(t);
        }

        var scopePrev = Tag.BeginScope(Tag.NewScope(merged));
        try
        {
            ILogger? logger = null;
            try
            {
                var sp = Di.CovenExecutionScope.CurrentProvider;
                var lf = sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                logger = lf?.CreateLogger("Coven.Ritual.Pull");
                logger?.LogInformation("Pull begin: input={InputType} branch={Branch}", input.GetType().Name, bid);
            }
            catch { }

            var selector = selectionStrategy ?? new DefaultSelectionStrategy();
            var engine = new SelectionEngine(registeredBlocks, selector);
            var fence = Tag.GetFenceForCurrentEpoch();
            var chosen = engine.SelectNext(input, fence, lastIndex: -1, forwardOnly: false)
                ?? throw new InvalidOperationException($"No next step available from type {input.GetType().Name}.");

            logger?.LogInformation("Pull select {Block} idx={Idx}", chosen.BlockTypeName, chosen.RegistryIndex);
            Tag.IncrementEpoch();
            // Invoke wrapped pull delegate which will finalize via Board.FinalizePullStep<T>
            await chosen.InvokePull(this, sink, bid, input).ConfigureAwait(false);
            logger?.LogInformation("Pull dispatched {Block}", chosen.BlockTypeName);
        }
        finally
        {
            Tag.EndScope(scopePrev);
        }
    }

    private static IReadOnlyList<RegisteredBlock> BuildRegisteredBlocks(IReadOnlyList<MagikBlockDescriptor> registry)
    {
        var list = new List<RegisteredBlock>(registry.Count);
        for (int idx = 0; idx < registry.Count; idx++)
        {
            var d = registry[idx];
            var block = d.BlockInstance;
            var name = d.DisplayBlockTypeName ?? block.GetType().Name;

            var caps = (block as ITagCapabilities)?.SupportedTags ?? Array.Empty<string>();
            IEnumerable<string> merged = caps;
            if (d.Capabilities is not null && d.Capabilities.Count > 0)
            {
                merged = System.Linq.Enumerable.Concat(caps, d.Capabilities);
            }
            var set = new HashSet<string>(merged, StringComparer.OrdinalIgnoreCase);
            // Soft self-capability to enable forward-motion hints via next:<BlockTypeName>
            set.Add($"next:{name}");

            var activator = d.Activator;
            if (activator is null)
            {
                var implementsMagik = block.GetType().GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMagikBlock<,>));
                if (implementsMagik) activator = new ConstantInstanceActivator(block);
                else throw new InvalidOperationException($"Coven configuration error: Missing activator for block entry at index {idx} ({name}). Either provide an IMagikBlock instance or an IBlockActivator.");
            }

            list.Add(new RegisteredBlock
            {
                Descriptor = d,
                RegistryIndex = idx,
                Invoke = BlockInvokerFactory.Create(d),
                InvokePull = static (_, __, ___, ____) => Task.CompletedTask,
                InputType = d.InputType,
                OutputType = d.OutputType,
                BlockTypeName = name,
                Capabilities = set,
                Activator = activator
            });
        }
        // Compute forward-next hint tags per block and compile pull wrappers using them
        for (int i = 0; i < list.Count; i++)
        {
            var cur = list[i];
            var tags = new List<string>();
            for (int j = i + 1; j < list.Count; j++)
            {
                var cand = list[j];
                if (cand.InputType.IsAssignableFrom(cur.OutputType))
                {
                    tags.Add($"next:{cand.BlockTypeName}");
                }
            }
            cur.ForwardNextTags = tags;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var cur = list[i];
            // Compile a pull wrapper that resolves the instance via activator, invokes, then finalizes with generic TOut.
            var finalize = typeof(Board).GetMethod("FinalizePullStep", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var finalizeClosed = finalize.MakeGenericMethod(cur.OutputType);
            cur.InvokePull = async (board, sink, branchId, input) =>
            {
                var cache = new Dictionary<int, object>();
                var sp = Di.CovenExecutionScope.CurrentProvider;
                var instance = cur.Activator.GetInstance(sp, cache, cur);
                var result = await cur.Invoke(instance, input).ConfigureAwait(false);
                finalizeClosed.Invoke(board, new object?[] { sink, result, branchId, cur.BlockTypeName, cur.ForwardNextTags });
            };
        }
        return list;
    }

    public bool WorkSupported<T>(List<string> tags)
    {
        // Until pull mode and richer admission logic are implemented, assume supported.
        // Routing will determine viability at execution time based on tags and capabilities.
        return true;
    }

    public Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink)
    {
        if (currentMode != BoardMode.Pull)
            throw new NotSupportedException("GetWork<TIn>(request) is only available in pull mode.");
        if (sink is null) throw new ArgumentNullException(nameof(sink));

        // Execute exactly one step using Push selection semantics, no forward-only constraint.
        return ExecutePullStepAsync(request, sink);
    }

    private async Task ExecutePullStepAsync<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink)
    {
        var branchId = request.BranchId ?? string.Empty;
        await GetWorkPullAsync(request.Input!, branchId, request.Tags, sink).ConfigureAwait(false);
    }

    public async Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null)
    {
        var startType = typeof(T);
        var targetType = typeof(TOutput);

        if (currentMode == BoardMode.Push)
        {
            var pipeline = (Func<T, Task<TOutput>>)pipelineCache.GetOrAdd(
                (startType, targetType),
                _ => compiler.Compile<T, TOutput>(startType, targetType)
            );

            var prev = Tag.BeginScope(Tag.NewScope(tags));
            try
            {
                return await pipeline(input);
            }
            finally
            {
                Tag.EndScope(prev);
            }
        }

        // Pull mode: delegate to the concrete orchestrator using GetWork<T> steps.
        var orchestrator = new PullOrchestrator(this, pullOptions);
        return await orchestrator.Run<T, TOutput>(input, tags);
    }

    internal void PrecompileAllPipelines()
    {
        var types = new HashSet<Type>();
        foreach (var d in Registry)
        {
            types.Add(d.InputType);
            types.Add(d.OutputType);
        }

        var compileMethod = typeof(PipelineCompiler).GetMethod("Compile", BindingFlags.NonPublic | BindingFlags.Instance);
        if (compileMethod is null) return;

        foreach (var start in types)
        {
            foreach (var target in types)
            {
                if (!compiler.PathExists(start, target)) continue;
                var gm = compileMethod.MakeGenericMethod(start, target);
                var del = (Delegate)gm.Invoke(compiler, new object[] { start, target })!;
                pipelineCache.TryAdd((start, target), del);
            }
        }
    }

    // Called by compiled pull-mode delegates to finalize a step with strict generic type
    internal void FinalizePullStep<TOut>(IOrchestratorSink sink, TOut output, string? branchId, string blockTypeName, IEnumerable<string> forwardNextTags)
    {
        Tag.Add($"by:{blockTypeName}");
        if (forwardNextTags is not null)
        {
            foreach (var t in forwardNextTags) Tag.Add(t);
        }
        var bid = branchId ?? string.Empty;
        // Persist tags for the next selection using only tags emitted during this step
        // plus fresh forward-next hints.
        // - Keep only current-epoch tags, excluding observational or computed hints (by:*, next:*)
        // - Re-add the newly computed forwardNextTags (next:*) for default forward bias
        var persisted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var epoch = Tag.CurrentEpochTags();
        foreach (var t in epoch)
        {
            if (t.StartsWith("by:", StringComparison.OrdinalIgnoreCase)) continue;
            if (t.StartsWith("next:", StringComparison.OrdinalIgnoreCase)) continue;
            persisted.Add(t);
        }
        if (forwardNextTags is not null)
        {
            foreach (var t in forwardNextTags) persisted.Add(t);
        }
        pullBranchTags[bid] = persisted;
        try
        {
            var sp = Di.CovenExecutionScope.CurrentProvider;
            var lf = sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = lf?.CreateLogger("Coven.Ritual.Pull");
            logger?.LogInformation("Pull complete {Block} => {OutputType} branch={Branch}", blockTypeName, output?.GetType().Name ?? "null", bid);
        }
        catch { }
        sink.Complete<TOut>(output, branchId);
    }
}
