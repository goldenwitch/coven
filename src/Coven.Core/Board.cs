using System.Collections.Concurrent;
using System.Reflection;
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

    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry)
    {
        currentMode = mode;
        Registry = registry;
        registeredBlocks = BuildRegisteredBlocks(registry);
        compiler = new PipelineCompiler(registeredBlocks);
    }
    
    internal Board(BoardMode mode, IReadOnlyList<MagikBlockDescriptor> registry, bool precompile)
    {
        currentMode = mode;
        Registry = registry;
        registeredBlocks = BuildRegisteredBlocks(registry);
        compiler = new PipelineCompiler(registeredBlocks);
        if (precompile)
        {
            PrecompileAllPipelines();
        }
    }

    private readonly ConcurrentDictionary<(Type start, Type target), Delegate> pipelineCache = new();

    private static IReadOnlyList<RegisteredBlock> BuildRegisteredBlocks(IReadOnlyList<MagikBlockDescriptor> registry)
    {
        var list = new List<RegisteredBlock>(registry.Count);
        for (int idx = 0; idx < registry.Count; idx++)
        {
            var d = registry[idx];
            var block = d.BlockInstance;
            var name = block.GetType().Name;

            var caps = (block as ITagCapabilities)?.SupportedTags ?? Array.Empty<string>();
            IEnumerable<string> merged = caps;
            if (d.Capabilities is not null && d.Capabilities.Count > 0)
            {
                merged = System.Linq.Enumerable.Concat(caps, d.Capabilities);
            }
            var set = new HashSet<string>(merged, StringComparer.OrdinalIgnoreCase);

            list.Add(new RegisteredBlock
            {
                Descriptor = d,
                RegistryIndex = idx,
                Invoke = BlockInvokerFactory.Create(d),
                InputType = d.InputType,
                OutputType = d.OutputType,
                BlockTypeName = name,
                Capabilities = set
            });
        }
        return list;
    }

    public bool WorkSupported<T>(List<string> tags)
    {
        // Until pull mode and richer admission logic are implemented, assume supported.
        // Routing will determine viability at execution time based on tags and capabilities.
        return true;
    }

    public Task<TOutput> GetWork<T, TOutput>(T input, List<string>? tags = null)
    {
        throw new NotImplementedException();
    }

    public async Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null)
    {
        if (currentMode == BoardMode.Push)
        {
            var startType = typeof(T);
            var targetType = typeof(TOutput);

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

        throw new NotSupportedException("Pull mode is not implemented.");
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
}
