namespace Coven.Core.Builder;

public class MagikBlockRegistration<T, TOutput> : IMagikBuilder<T, TOutput>
{
    private readonly IMagikBuilder<T, TOutput> magikBuilder;
    private readonly IMagikBlock<T, TOutput> block;

    internal MagikBlockRegistration(IMagikBuilder<T, TOutput> magikBuilder, IMagikBlock<T, TOutput> block)
    {
        this.magikBuilder = magikBuilder;
        this.block = block;
    }

    public ICoven Done()
    {
        return magikBuilder.Done();
    }

    public MagikBlockRegistration<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block)
    {
        return magikBuilder.MagikBlock(block);
    }

    public MagikBlockRegistration<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func)
    {
        return magikBuilder.MagikBlock(func);
    }
}
