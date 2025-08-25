namespace Coven.Core.Builder;

public interface IMagikBuilder<T, TOutput>
{
    public MagikBlockRegistration<T, TOutput> MagikBlock(IMagikBlock<T, TOutput> block);

    public MagikBlockRegistration<T, TOutput> MagikBlock(Func<T, Task<TOutput>> func);

    public ICoven Done();
}
