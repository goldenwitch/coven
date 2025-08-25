using System.Collections.Generic;

namespace Coven.Core.Builder;

public class MagikBuilder<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly List<MagikBlockDescriptor> registry = new();

    public MagikBlockRegistration<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block)
    {
        registry.Add(new MagikBlockDescriptor(typeof(T), typeof(TOutput), block));
        return new MagikBlockRegistration<T, TOutput>(this, block);
    }

    public MagikBlockRegistration<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func)
    {
        var mb = new MagikBlock<T, TOutput>(func);
        registry.Add(new MagikBlockDescriptor(typeof(T), typeof(TOutput), mb));
        return new MagikBlockRegistration<T, TOutput>(this, mb);
    }

    public ICoven Done()
    {
        // Build a Board in push mode with precompiled pipelines
        var board = new Board(Board.BoardMode.Push, registry.AsReadOnly(), precompile: true);

        return new Coven(board);
    }
}
