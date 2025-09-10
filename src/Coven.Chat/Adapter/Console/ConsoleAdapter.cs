namespace Coven.Chat.Adapter.Console;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ConsoleAdapter : IAdapter<ChatEntry>
{
    private readonly IConsoleIO _io;
    private readonly ConsoleAdapterOptions _options;
    private readonly ILogger<ConsoleAdapter> _log;

    public ConsoleAdapter(IConsoleIO io, ConsoleAdapterOptions? options = null)
        : this(io, options, NullLogger<ConsoleAdapter>.Instance)
    { }

    public ConsoleAdapter(IConsoleIO io, ConsoleAdapterOptions? options, ILogger<ConsoleAdapter> logger)
    {
        _io = io ?? throw new ArgumentNullException(nameof(io));
        _options = options ?? new ConsoleAdapterOptions();
        _log = logger ?? NullLogger<ConsoleAdapter>.Instance;
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
            _log.LogDebug("chat:console read line bytes={Len}", line.Length);
            if (_options.EchoUserInput)
            {
                await _io.WriteLineAsync(line, ct).ConfigureAwait(false);
            }
        }
    }

    public async Task DeliverAsync(ChatEntry entry, CancellationToken ct = default)
    {
        switch (entry)
        {
            case ChatResponse r:
                await _io.WriteLineAsync(r.Text, ct).ConfigureAwait(false);
                _log.LogDebug("chat:console wrote response bytes={Len}", r.Text?.Length ?? 0);
                break;
            default:
                // Ignore other entry types by default to avoid echo loops
                _log.LogTrace("chat:console skipped type={Type}", entry?.GetType().Name);
                break;
        }
    }
}
