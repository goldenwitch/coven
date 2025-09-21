// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Coven.Core.Logging;
using Coven.Core.Routing;
using Coven.Core.Tags;
using Coven.Core.Activation;
using Coven.Core.Builder;

namespace Coven.Core;

public class Board : IBoard
{
    internal enum BoardMode
    {
        Push,
        Pull
    }

    private readonly BoardMode _currentMode;
    internal IReadOnlyList<MagikBlockDescriptor> Registry { get; }
    private readonly IReadOnlyList<RegisteredBlock> _registeredBlocks;
    private readonly PipelineCompiler _compiler;
    private readonly ConcurrentDictionary<string, HashSet<string>> _pullBranchTags = new(StringComparer.Ordinal);
    private readonly PullOptions? _pullOptions;
    private readonly ISelectionStrategy? _selectionStrategy;

    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry, PullOptions? pullOptions = null, ISelectionStrategy? selectionStrategy = null)
    {
        _currentMode = mode;
        Registry = registry;
        _registeredBlocks = BuildRegisteredBlocks(registry);
        _selectionStrategy = selectionStrategy;
        _compiler = new PipelineCompiler(_registeredBlocks, selectionStrategy);
        _pullOptions = pullOptions;

        // Always precompile pipelines regardless of mode for consistency
        PrecompileAllPipelines();
    }

    private readonly ConcurrentDictionary<(Type start, Type target), Delegate> _pipelineCache = new();

    // Minimal internal status snapshot for tests and diagnostics.
    internal BoardStatus Status => new(_pipelineCache.Count);

    // Internal non-generic step for orchestrators; merges tags, selects, executes, persists tags, returns output.
    internal async Task GetWorkPullAsync(object input, string? branchId, IReadOnlyCollection<string>? extraTags, IOrchestratorSink sink, CancellationToken cancellationToken)
    {
        if (_currentMode != BoardMode.Pull)
        {
            throw new NotSupportedException("GetWork is only available in pull mode.");
        }


        string bid = branchId ?? string.Empty;
        HashSet<string> baseTags = _pullBranchTags.GetOrAdd(bid, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        HashSet<string> merged = new(baseTags, StringComparer.OrdinalIgnoreCase);
        if (extraTags is not null)
        {
            foreach (string t in extraTags)
            {
                _ = merged.Add(t);
            }

        }

        ITagScope? scopePrev = Tag.BeginScope(Tag.NewScope(merged));
        try
        {
            ILogger? logger = null;
            IDisposable? ritualScope = null;
            IServiceProvider? sp = CovenExecutionScope.CurrentProvider;
            ILoggerFactory? lf = sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            logger = lf?.CreateLogger("Coven.Ritual.Pull");
            string rid = Guid.NewGuid().ToString("N");
            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                ritualScope = logger.BeginScope("ritual:" + rid);
                CoreLog.PullBegin(logger, rid, input.GetType().Name, bid);
            }

            ISelectionStrategy selector = _selectionStrategy ?? new DefaultSelectionStrategy();
            SelectionEngine engine = new(_registeredBlocks, selector);
            IReadOnlyCollection<object>? fence = Tag.GetFenceForCurrentEpoch();
            RegisteredBlock chosen = engine.SelectNext(input, fence, lastIndex: -1, forwardOnly: false)
                ?? throw new InvalidOperationException($"No next step available from type {input.GetType().Name}.");

            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                CoreLog.PullSelect(logger, chosen.BlockTypeName, chosen.RegistryIndex);
            }
            Tag.IncrementEpoch();
            // Invoke wrapped pull delegate which will finalize via Board.FinalizePullStep<T>
            await chosen.InvokePull(this, sink, bid, input, cancellationToken).ConfigureAwait(false);
            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                CoreLog.PullDispatched(logger, chosen.BlockTypeName);
            }
            ritualScope?.Dispose();
        }
        finally
        {
            Tag.EndScope(scopePrev);
        }
    }

    private static List<RegisteredBlock> BuildRegisteredBlocks(IReadOnlyList<MagikBlockDescriptor> registry)
    {
        List<RegisteredBlock> list = new(registry.Count);
        for (int idx = 0; idx < registry.Count; idx++)
        {
            MagikBlockDescriptor d = registry[idx];
            object block = d.BlockInstance;
            IBlockActivator? activator = d.Activator;
            string name = d.DisplayBlockTypeName ?? activator?.DisplayName ?? block.GetType().Name;

            // Capabilities come solely from descriptor (set at DI registration)
            IEnumerable<string> merged = d.Capabilities ?? Enumerable.Empty<string>();
            HashSet<string> set = new(merged, StringComparer.OrdinalIgnoreCase)
            {
                // Soft self-capability to enable forward-motion hints via next:<BlockTypeName>
                $"next:{name}"
            };

            if (activator is null)
            {
                bool implementsMagik = block.GetType().GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMagikBlock<,>));
                activator = implementsMagik
                    ? (IBlockActivator)new ConstantInstanceActivator(block)
                    : throw new InvalidOperationException($"Coven configuration error: Missing activator for block entry at index {idx} ({name}). Either provide an IMagikBlock instance or an IBlockActivator.");

            }

            list.Add(new RegisteredBlock
            {
                Descriptor = d,
                RegistryIndex = idx,
                Invoke = BlockInvokerFactory.Create(d),
                InvokePull = static (_, __, ___, ____, _____) => Task.CompletedTask,
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
            RegisteredBlock cur = list[i];
            List<string> tags = [];
            for (int j = i + 1; j < list.Count; j++)
            {
                RegisteredBlock cand = list[j];
                if (cand.InputType.IsAssignableFrom(cur.OutputType))
                {
                    tags.Add($"next:{cand.BlockTypeName}");
                }
            }
            cur.ForwardNextTags = tags;
        }

        for (int i = 0; i < list.Count; i++)
        {
            RegisteredBlock cur = list[i];
            // Compile a pull wrapper that resolves the instance via activator, invokes, then finalizes with generic TOut.
            MethodInfo finalize = typeof(Board).GetMethod("FinalizePullStep", BindingFlags.Instance | BindingFlags.NonPublic)!;
            MethodInfo finalizeClosed = finalize.MakeGenericMethod(cur.OutputType);
            cur.InvokePull = async (board, sink, branchId, input, cancellationToken) =>
            {
                Dictionary<int, object> cache = [];
                IServiceProvider? sp = CovenExecutionScope.CurrentProvider;
                object instance = cur.Activator.GetInstance(sp, cache, cur);
                object result = await cur.Invoke(instance, input, cancellationToken).ConfigureAwait(false);
                _ = finalizeClosed.Invoke(board, [sink, result, branchId, cur.BlockTypeName, cur.ForwardNextTags]);
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

    public Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink, CancellationToken cancellationToken = default)
    {
        if (_currentMode != BoardMode.Pull)
        {

            throw new NotSupportedException("GetWork<TIn>(request) is only available in pull mode.");
        }


        ArgumentNullException.ThrowIfNull(sink);

        // Execute exactly one step using Push selection semantics, no forward-only constraint.
        return ExecutePullStepAsync(request, sink, cancellationToken);
    }

    private async Task ExecutePullStepAsync<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink, CancellationToken cancellationToken)
    {
        string branchId = request.BranchId ?? string.Empty;
        await GetWorkPullAsync(request.Input!, branchId, request.Tags, sink, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null, CancellationToken cancellationToken = default)
    {
        Type startType = typeof(T);
        Type targetType = typeof(TOutput);

        if (_currentMode == BoardMode.Push)
        {
            Func<T, CancellationToken, Task<TOutput>> pipeline = (Func<T, CancellationToken, Task<TOutput>>)_pipelineCache.GetOrAdd(
                (startType, targetType),
                _ => _compiler.Compile<T, TOutput>(startType, targetType)
            );

            ITagScope? prev = Tag.BeginScope(Tag.NewScope(tags));
            try
            {
                return await pipeline(input, cancellationToken);
            }
            finally
            {
                Tag.EndScope(prev);
            }
        }

        // Pull mode: delegate to the concrete orchestrator using GetWork<T> steps.
        PullOrchestrator orchestrator = new(this, _pullOptions);
        return await orchestrator.Run<T, TOutput>(input, tags, cancellationToken);
    }

    internal void PrecompileAllPipelines()
    {
        HashSet<Type> types = [];
        foreach (MagikBlockDescriptor d in Registry)
        {
            _ = types.Add(d.InputType);
            _ = types.Add(d.OutputType);
        }

        MethodInfo? compileMethod = typeof(PipelineCompiler).GetMethod("Compile", BindingFlags.NonPublic | BindingFlags.Instance);
        if (compileMethod is null)
        {
            return;
        }


        foreach (Type start in types)
        {
            foreach (Type target in types)
            {
                if (!_compiler.PathExists(start, target))
                {
                    continue;
                }


                MethodInfo gm = compileMethod.MakeGenericMethod(start, target);
                Delegate del = (Delegate)gm.Invoke(_compiler, [start, target])!;
                _ = _pipelineCache.TryAdd((start, target), del);
            }
        }
    }

    // Called by compiled pull-mode delegates to finalize a step with strict generic type
    internal void FinalizePullStep<TOut>(IOrchestratorSink sink, TOut output, string? branchId, string blockTypeName, IEnumerable<string> forwardNextTags)
    {
        Tag.Add($"by:{blockTypeName}");
        if (forwardNextTags is not null)
        {
            foreach (string t in forwardNextTags)
            {
                Tag.Add(t);
            }

        }
        string bid = branchId ?? string.Empty;
        // Persist tags for the next selection using only tags emitted during this step
        // plus fresh forward-next hints.
        // - Keep only current-epoch tags, excluding observational or computed hints (by:*, next:*)
        // - Re-add the newly computed forwardNextTags (next:*) for default forward bias
        HashSet<string> persisted = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> epoch = Tag.CurrentEpochTags();
        foreach (string t in epoch)
        {
            if (t.StartsWith("by:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }


            if (t.StartsWith("next:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }


            _ = persisted.Add(t);
        }
        if (forwardNextTags is not null)
        {
            foreach (string t in forwardNextTags)
            {
                _ = persisted.Add(t);
            }

        }
        _pullBranchTags[bid] = persisted;
        IServiceProvider? sp = CovenExecutionScope.CurrentProvider;
        ILoggerFactory? lf = sp?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        ILogger? logger = lf?.CreateLogger("Coven.Ritual.Pull");
        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            CoreLog.PullComplete(logger, blockTypeName, output?.GetType().Name ?? "null", bid);
        }
        sink.Complete(output, branchId);
    }
}
