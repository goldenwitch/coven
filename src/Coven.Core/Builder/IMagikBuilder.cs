namespace Coven.Core.Builder;

public interface IMagikBuilder<T, TOutput>
{
    public IMagikBuilder<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block);

    public IMagikBuilder<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func);

    // Overloads with capabilities set at registration
    public IMagikBuilder<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block, IEnumerable<string> capabilities);

    public IMagikBuilder<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func, IEnumerable<string> capabilities);

    // Heterogeneous registration: allow adding blocks with any input/output
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block);

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func);

    // Heterogeneous with capabilities
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string> capabilities);

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string> capabilities);

    public ICoven Done();
}
