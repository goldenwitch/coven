using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Tests.Infrastructure;

public interface ITestTailMux : IAsyncDisposable
{
    Task TailAsync(Func<string, bool, ValueTask> onMessage, CancellationToken ct = default);
    Task WriteLineAsync(string line, CancellationToken ct = default);
    object Underlying { get; }
}

internal sealed class MuxAdapter : ITestTailMux
{
    private readonly ITailMux _inner;
    public MuxAdapter(ITailMux inner) { _inner = inner; }
    public Task TailAsync(Func<string, bool, ValueTask> onMessage, CancellationToken ct = default)
        => _inner.TailAsync(ev => onMessage(ev.Line, ev is ErrorLine), ct);
    public Task WriteLineAsync(string line, CancellationToken ct = default)
        => _inner.WriteLineAsync(line, ct);
    public object Underlying => _inner;
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

