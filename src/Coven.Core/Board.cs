using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Coven.Core.Algos;

namespace Coven.Core;

public class Board : IBoard
{
    internal enum BoardMode
    {
        Push,
        Pull
    }

    private readonly BoardMode currentMode = BoardMode.Push;
    public IReadOnlyList<MagikBlockDescriptor> Registry { get; }

    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry)
    {
        currentMode = mode;
        Registry = registry;
    }
    
    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry, bool precompile)
    {
        currentMode = mode;
        Registry = registry;
        if (precompile)
        {
            PrecompileAllPipelines();
        }
    }

    private readonly ConcurrentDictionary<(Type start, Type target), Delegate> pipelineCache = new();

    public Task<TOutput> GetWork<T, TOutput>(T input, List<string>? tags = null)
    {
        throw new NotImplementedException();
    }

    public async Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null)
    {
        // If we are in push mode we can evaluate the whole chain and get the promise directly from the end node.
        if (currentMode == BoardMode.Push)
        {
            var startType = typeof(T);
            var targetType = typeof(TOutput);

            if (targetType.IsAssignableFrom(startType))
            {
                return (TOutput)(object)input!;
            }

            var chain = FindChain(startType, targetType);
            if (chain is null || chain.Count == 0)
            {
                throw new InvalidOperationException($"No chain found from {startType} to {targetType}.");
            }

            var pipeline = (Func<T, Task<TOutput>>)pipelineCache.GetOrAdd(
                (startType, targetType),
                _ => CompilePipeline<T, TOutput>(chain)
            );

            return await pipeline(input);
        }


        // Pull mode is not implemented yet
        throw new NotSupportedException("Pull mode is not implemented.");
    }

    internal void PrecompileAllPipelines()
    {
        // Build unique type set from registry
        var types = new HashSet<Type>();
        foreach (var d in Registry)
        {
            types.Add(d.InputType);
            types.Add(d.OutputType);
        }

        var compileMethod = typeof(Board).GetMethod("CompilePipeline", BindingFlags.Static | BindingFlags.NonPublic);
        if (compileMethod is null) return;

        foreach (var start in types)
        {
            foreach (var target in types)
            {
                if (target.IsAssignableFrom(start))
                {
                    // PostWork fast-path will handle identity; skip
                    continue;
                }
                var chain = FindChain(start, target);
                if (chain is null || chain.Count == 0) continue;
                var gm = compileMethod.MakeGenericMethod(start, target);
                var del = (Delegate)gm.Invoke(null, new object[] { chain })!;
                pipelineCache.TryAdd((start, target), del);
            }
        }
    }

    // Finds the shortest valid chain of blocks that transform startType into targetType.
    // Returns the ordered list of MagikBlocks to invoke.
    private List<MagikBlockDescriptor>? FindChain(Type startType, Type targetType, int maxDepth = 20)
    {
        if (targetType.IsAssignableFrom(startType))
        {
            return new List<MagikBlockDescriptor>();
        }

        var indexed = Registry.Select((d, idx) => (descriptor: d, index: idx)).ToArray();
        var indexMap = new Dictionary<MagikBlockDescriptor, int>();
        foreach (var e in indexed) indexMap[e.descriptor] = e.index;

        return GraphSearch.BfsEdges<Type, MagikBlockDescriptor, Dictionary<Type, int>>(
            start: startType,
            isGoal: t => targetType.IsAssignableFrom(t),
            expand: current =>
                indexed
                    .Where(e => e.descriptor.InputType.IsAssignableFrom(current))
                    .Select(e => (e.descriptor.OutputType, e.descriptor)),
            buildAnnotation: current => Distance.AllMinHops(
                current,
                t =>
                {
                    var list = new List<Type>();
                    if (t.BaseType is not null) list.Add(t.BaseType);
                    list.AddRange(t.GetInterfaces());
                    return list;
                }),
            orderNeighbors: (current, distMap, neighbors) => neighbors
                .OrderBy(n => distMap.TryGetValue(n.edge.InputType, out var d) ? d : int.MaxValue)
                .ThenBy(n => indexMap[n.edge]),
            comparer: null,
            maxDepth: maxDepth
        );
    }


    private static Func<T, Task<TOutput>> CompilePipeline<T, TOutput>(List<MagikBlockDescriptor> chain)
    {
        // Build expression: (T input) => Then( Then( block0.DoMagik((T0)input), v0 => block1.DoMagik((T1)v0) ), ... )
        var param = Expression.Parameter(typeof(T), "input");

        Expression currentTaskExpr = null!;
        Type currentOutType = typeof(T);

        for (int i = 0; i < chain.Count; i++)
        {
            var step = chain[i];
            var blockConst = Expression.Constant(step.BlockInstance);
            var doMagik = step.BlockInstance.GetType().GetMethod("DoMagik", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (doMagik is null)
            {
                throw new InvalidOperationException($"Block {step.BlockInstance.GetType().Name} does not implement DoMagik.");
            }

            var expectedIn = step.InputType;
            var expectedOut = step.OutputType;

            if (i == 0)
            {
                // First call: cast input to expectedIn
                Expression arg = param;
                if (expectedIn != typeof(T))
                {
                    if (!expectedIn.IsAssignableFrom(typeof(T)))
                        throw new InvalidOperationException($"Pipeline mismatch: cannot cast {typeof(T)} to {expectedIn} for block {step.BlockInstance.GetType().Name}.");
                    arg = Expression.Convert(param, expectedIn);
                }
                currentTaskExpr = Expression.Call(blockConst, doMagik, arg); // Task<expectedOut>
                currentOutType = expectedOut;
            }
            else
            {
                // Chain using Then<Task>
                if (!expectedIn.IsAssignableFrom(currentOutType))
                {
                    throw new InvalidOperationException($"Pipeline mismatch: block expects {expectedIn} but previous block outputs {currentOutType}.");
                }

                var prevType = currentOutType;
                var nextType = expectedOut;

                // Build continuation: (Task<prevType> t) => block.DoMagik((expectedIn) t.Result) // returns Task<nextType>
                var tParam = Expression.Parameter(typeof(Task<>).MakeGenericType(prevType), "t");
                Expression contArg = Expression.Property(tParam, nameof(Task<object>.Result)); // t.Result
                if (expectedIn != prevType)
                {
                    contArg = Expression.Convert(contArg, expectedIn);
                }
                var contBody = Expression.Call(blockConst, doMagik, contArg); // Task<nextType>
                var contLambda = Expression.Lambda(contBody, tParam); // Func<Task<prevType>, Task<nextType>>

                // currentTaskExpr.ContinueWith<Task<nextType>>(contLambda).Unwrap()
                var taskPrevType = typeof(Task<>).MakeGenericType(prevType);
                var continueWithGeneric = taskPrevType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.Name == "ContinueWith" && m.IsGenericMethodDefinition)
                    .Select(m => new { m, ps = m.GetParameters() })
                    .Where(x => x.ps.Length == 1 && x.ps[0].ParameterType.IsGenericType)
                    .Select(x => x.m)
                    .First();
                var taskNextType = typeof(Task<>).MakeGenericType(nextType);
                var continueCall = Expression.Call(currentTaskExpr, continueWithGeneric.MakeGenericMethod(taskNextType), contLambda);

                var unwrapMethod = typeof(System.Threading.Tasks.TaskExtensions)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(mi => mi.Name == "Unwrap" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 1);
                var unwrapCall = Expression.Call(unwrapMethod.MakeGenericMethod(nextType), continueCall); // Task<nextType>

                currentTaskExpr = unwrapCall;
                currentOutType = nextType;
            }
        }

        if (currentTaskExpr is null)
        {
            // No steps; identity pipeline is handled earlier by caller.
            throw new InvalidOperationException("Cannot compile empty pipeline.");
        }

        if (!typeof(TOutput).IsAssignableFrom(currentOutType))
        {
            throw new InvalidOperationException($"Final pipeline type {currentOutType} not assignable to {typeof(TOutput)}.");
        }

        // If the pipeline's last Task type is a subtype of TOutput, insert a mapping ContinueWith.
        if (currentOutType != typeof(TOutput))
        {
            var taskFinalType = typeof(Task<>).MakeGenericType(currentOutType);
            var contParam = Expression.Parameter(taskFinalType, "t");
            var resultProp = Expression.Property(contParam, nameof(Task<object>.Result));
            var body = Expression.Convert(resultProp, typeof(TOutput));
            var mapLambda = Expression.Lambda(body, contParam); // Func<Task<currentOutType>, TOutput>

            var contGeneric = taskFinalType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == "ContinueWith" && m.IsGenericMethodDefinition)
                .Select(m => new { m, ps = m.GetParameters() })
                .Where(x => x.ps.Length == 1)
                .Select(x => x.m)
                .First();

            currentTaskExpr = Expression.Call(currentTaskExpr, contGeneric.MakeGenericMethod(typeof(TOutput)), mapLambda); // Task<TOutput>
        }

        var finalLambda = Expression.Lambda<Func<T, Task<TOutput>>>(currentTaskExpr, param);
        return finalLambda.Compile();
    }

    public bool WorkSupported<T>(List<string> tags)
    {
        // Until pull mode and tag semantics are implemented, assume supported.
        return true;
    }
}
