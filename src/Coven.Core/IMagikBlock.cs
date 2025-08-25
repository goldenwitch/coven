namespace Coven.Core;

public interface IMagikBlock<T, TOutput>
{
    Task<TOutput> DoMagik(T input);
}
