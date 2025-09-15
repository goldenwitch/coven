// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public interface ITestTailMux : IAsyncDisposable
{
    Task TailAsync(Func<string, bool, ValueTask> onMessage, CancellationToken ct = default);
    Task WriteAsync(string data, CancellationToken ct = default);
    object Underlying { get; }
}

internal sealed class MuxAdapter : ITestTailMux
{
    private readonly ITailMux _inner;
    public MuxAdapter(ITailMux inner) { _inner = inner; }
    public Task TailAsync(Func<string, bool, ValueTask> onMessage, CancellationToken ct = default)
        => _inner.TailAsync(ev => onMessage(ev.Line, ev is ErrorLine), ct);
    public Task WriteAsync(string data, CancellationToken ct = default)
        => _inner.WriteAsync(data, ct);
    public object Underlying => _inner;
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
