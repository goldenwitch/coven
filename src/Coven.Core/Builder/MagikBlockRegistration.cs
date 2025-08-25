namespace Coven.Core.Builder;

internal class MagikBlockRegistration<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly IMagikBuilder<T, TOutput> magikBuilder;

    internal MagikBlockRegistration(IMagikBuilder<T, TOutput> magikBuilder)
    {
        this.magikBuilder = magikBuilder;
    }

    public ICoven Done()
    {
        return magikBuilder.Done();
    }

    public IMagikBuilder<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block)
    {
        return magikBuilder.MagikBlock(block);
    }

    public IMagikBuilder<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func)
    {
        return magikBuilder.MagikBlock(func);
    }

    // Heterogeneous registration passthroughs
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block)
    {
        return magikBuilder.MagikBlock(block);
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func)
    {
        return magikBuilder.MagikBlock(func);
    }

}
