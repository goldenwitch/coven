namespace Coven.Chat.Adapter.Console;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public sealed class ConsoleAdapter : IAdapter<ChatEntry>
{
    private readonly IConsoleIO _io;
    private readonly ConsoleAdapterOptions _options;

    public ConsoleAdapter(IConsoleIO io, ConsoleAdapterOptions? options = null)
    {
        _io = io ?? throw new ArgumentNullException(nameof(io));
        _options = options ?? new ConsoleAdapterOptions();
    }

    public async IAsyncEnumerable<ChatEntry> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            string? line;
            try { line = await _io.ReadLineAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { yield break; }

            if (line is null) yield break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            yield return new ChatThought(_options.InputSender, line);
            if (_options.EchoUserInput)
            {
                try { await _io.WriteLineAsync(line, ct).ConfigureAwait(false); } catch { }
            }
        }
    }

    public async Task DeliverAsync(ChatEntry entry, CancellationToken ct = default)
    {
        switch (entry)
        {
            case ChatResponse r:
                await _io.WriteLineAsync(r.Text, ct).ConfigureAwait(false);
                break;
            default:
                // Ignore other entry types by default to avoid echo loops
                break;
        }
    }
}
