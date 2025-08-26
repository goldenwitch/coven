namespace Coven.Core;

public interface IBoard
{
    public Task<TOutput> PostWork<T, TOutput>(T input, List<string>? tags = null);

    public Task GetWork<TIn>(GetWorkRequest<TIn> request, IOrchestratorSink sink);
    public bool WorkSupported<T>(List<string> tags);
}
