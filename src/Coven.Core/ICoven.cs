namespace Coven.Core;

public interface ICoven
{
    public Task<TOutput> Ritual<T, TOutput>(T Input);
}
