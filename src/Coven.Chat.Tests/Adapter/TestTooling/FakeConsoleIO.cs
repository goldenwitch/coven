namespace Coven.Chat.Tests.Adapter.TestTooling;

using Coven.Chat.Adapter.Console;
using System.Collections.Concurrent;
using System.Threading.Channels;

public sealed class FakeConsoleIO : IConsoleIO
{
    private readonly Channel<string> _in = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly ConcurrentQueue<string> _out = new();

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        var s = await _in.Reader.ReadAsync(ct).AsTask().ConfigureAwait(false);
        return s;
    }

    public Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        _out.Enqueue(line);
        return Task.CompletedTask;
    }

    // Test helpers
    public bool TryDequeueOutput(out string? line) => _out.TryDequeue(out line);
    public void EnqueueInput(string line) => _in.Writer.TryWrite(line);
}
