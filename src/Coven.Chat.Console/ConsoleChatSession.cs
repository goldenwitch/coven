// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal sealed class ConsoleChatSession(
    ConsoleGatewayConnection gateway,
    IScrivener<ConsoleEntry> consoleJournal,
    IScrivener<ChatEntry> chatJournal,
    IImbuingTransmuter<ConsoleEntry, long, ChatEntry> afferentTransmuter,
    IImbuingTransmuter<ChatEntry, long, ConsoleEntry> efferentTransmuter,
    ILogger<ConsoleChatSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly ConsoleGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<ConsoleEntry> _consoleJournal = consoleJournal ?? throw new ArgumentNullException(nameof(consoleJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IImbuingTransmuter<ConsoleEntry, long, ChatEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<ChatEntry, long, ConsoleEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _consoleToChatPump;
    private Task? _chatToConsolePump;

    public async Task StartAsync()
    {
        System.Diagnostics.Debug.WriteLine("[ConsoleChatSession] StartAsync called");
        System.Console.Error.WriteLine("[ConsoleChatSession] StartAsync called (stderr)");
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync(ct).ConfigureAwait(false);

        _consoleToChatPump = Task.Run(async () =>
        {
            System.Console.WriteLine("[ConsoleChatSession] _consoleToChatPump started, tailing console journal");
            try
            {
                await foreach ((long position, ConsoleEntry entry) in _consoleJournal.TailAsync(0, ct))
                {
                    System.Console.WriteLine($"[ConsoleChatSession] Received {entry.GetType().Name} at position {position}");
                    if (entry is ConsoleAck)
                    {
                        continue;
                    }

                    ConsoleLog.ConsoleToChatObserved(_logger, entry.GetType().Name, position);
                    ChatEntry chat = await _afferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    System.Console.WriteLine($"[ConsoleChatSession] Transmuted to {chat.GetType().Name}");
                    ConsoleLog.ConsoleToChatTransmuted(_logger, entry.GetType().Name, chat.GetType().Name);
                    long chatPos = await _chatJournal.WriteAsync(chat, ct).ConfigureAwait(false);
                    System.Console.WriteLine($"[ConsoleChatSession] Wrote to chat journal at position {chatPos}");
                    ConsoleLog.ConsoleToChatAppended(_logger, chat.GetType().Name, chatPos);
                }
                ConsoleLog.ConsoleToChatPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                ConsoleLog.ConsoleToChatPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                ConsoleLog.ConsoleToChatPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _chatToConsolePump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, ChatEntry entry) in _chatJournal.TailAsync(0, ct))
                {
                    // Forward only fixed ChatOutgoing to console
                    if (entry is not ChatEfferent)
                    {
                        continue;
                    }

                    ConsoleLog.ChatToConsoleObserved(_logger, entry.GetType().Name, position);
                    ConsoleEntry console = await _efferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    ConsoleLog.ChatToConsoleTransmuted(_logger, entry.GetType().Name, console.GetType().Name);
                    long consolePos = await _consoleJournal.WriteAsync(console, ct).ConfigureAwait(false);
                    ConsoleLog.ChatToConsoleAppended(_logger, console.GetType().Name, consolePos);
                }
                ConsoleLog.ChatToConsolePumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                ConsoleLog.ChatToConsolePumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                ConsoleLog.ChatToConsolePumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_consoleToChatPump is not null && _chatToConsolePump is not null)
            {
                try
                {
                    await Task.WhenAll(_consoleToChatPump, _chatToConsolePump).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during cooperative shutdown.
                }
            }
        }
        finally
        {
            await _gateway.DrainAsync().ConfigureAwait(false);
            _consoleToChatPump = null;
            _chatToConsolePump = null;
            GC.SuppressFinalize(this);
        }
    }
}
