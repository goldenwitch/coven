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

    public ICoven Done(bool pull)
    {
        return magikBuilder.Done(pull);
    }

    public ICoven Done(bool pull, PullOptions? pullOptions)
    {
        return magikBuilder.Done(pull, pullOptions);
    }

    // Heterogeneous registration passthroughs (with optional capabilities)
    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(IMagikBlock<TIn, TOut> block, IEnumerable<string>? capabilities = null)
    {
        return magikBuilder.MagikBlock(block, capabilities);
    }

    public IMagikBuilder<T, TOutput> MagikBlock<TIn, TOut>(Func<TIn, Task<TOut>> func, IEnumerable<string>? capabilities = null)
    {
        return magikBuilder.MagikBlock(func, capabilities);
    }

}
