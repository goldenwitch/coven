namespace Coven.Core.Builder;

public interface IMagikBuilder<T, TOutput>
{
    public IMagikBuilder<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block);

    public IMagikBuilder<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func);

    // Heterogeneous registration: allow adding blocks with any input/output
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block);

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func);

    public ICoven Done();
}
