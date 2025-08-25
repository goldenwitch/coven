namespace Coven.Core;

public interface IBoard
{
    public Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null);

    public Task<TOutput> GetWork<T, TOutput>(T input, List<string>? tags = null);
    public bool WorkSupported<T>(List<string> tags);
}
