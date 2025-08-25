using System.Collections.Generic;

namespace Coven.Core.Builder;

public class MagikBuilder<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly List<MagikBlockDescriptor> registry = new();

    

    public IMagikBuilder<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block)
    {
        registry.Add(new MagikBlockDescriptor(typeof(T), typeof(TOutput), block));
        return this;
    }

    public IMagikBuilder<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func)
    {
        var mb = new MagikBlock<T, TOutput>(func);
        registry.Add(new MagikBlockDescriptor(typeof(T), typeof(TOutput), mb));
        return this;
    }

    // Heterogeneous registration overloads
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block)
    {
        registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), block));
        return this;
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func)
    {
        var mb = new MagikBlock<TIn, TOut>(func);
        registry.Add(new MagikBlockDescriptor(typeof(TIn), typeof(TOut), mb));
        return this;
    }

    

    public ICoven Done()
    {
        // Build a Board in push mode with precompiled pipelines
        var board = new Board(Board.BoardMode.Push, registry.AsReadOnly(), precompile: true);

        return new Coven(board);
    }
}
