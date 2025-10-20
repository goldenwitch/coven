using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal static class ConsoleLog
{
    private static readonly Action<ILogger, Exception?> _connecting =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3000, nameof(Connecting)),
            "Console session connecting.");

    private static readonly Action<ILogger, Exception?> _connected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3001, nameof(Connected)),
            "Console session connected.");

    private static readonly Action<ILogger, int, Exception?> _outboundSendStart =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(3006, nameof(OutboundSendStart)),
            "Sending console line (length {Length}).");

    private static readonly Action<ILogger, Exception?> _outboundSendSucceeded =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(3007, nameof(OutboundSendSucceeded)),
            "Sent console line.");

    private static readonly Action<ILogger, Exception?> _outboundOperationCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3008, nameof(OutboundOperationCanceled)),
            "Outbound send canceled.");

    private static readonly Action<ILogger, Exception?> _outboundSendFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3009, nameof(OutboundSendFailed)),
            "Outbound send failed.");

    private static readonly Action<ILogger, string, int, Exception?> _inboundUserLineReceived =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(3010, nameof(InboundUserLineReceived)),
            "Inbound console line received from {Sender} (length {Length}).");

    private static readonly Action<ILogger, Exception?> _inboundEmptySkipped =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(3011, nameof(InboundEmptySkipped)),
            "Inbound empty/whitespace line skipped.");

    private static readonly Action<ILogger, string, long, Exception?> _inboundAppendedToJournal =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3012, nameof(InboundAppendedToJournal)),
            "Appended inbound {EntryType} to Console journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _consoleToChatObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3020, nameof(ConsoleToChatObserved)),
            "Console→Chat observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _consoleToChatTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3021, nameof(ConsoleToChatTransmuted)),
            "Console→Chat transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _consoleToChatAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3022, nameof(ConsoleToChatAppended)),
            "Console→Chat appended {EntryType} to Chat journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _chatToConsoleObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3030, nameof(ChatToConsoleObserved)),
            "Chat→Console observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _chatToConsoleTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3031, nameof(ChatToConsoleTransmuted)),
            "Chat→Console transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _chatToConsoleAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3032, nameof(ChatToConsoleAppended)),
            "Chat→Console appended {EntryType} to Console journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _consoleScrivenerAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3040, nameof(ConsoleScrivenerAppended)),
            "ConsoleScrivener appended {EntryType} to internal journal at position {Position}.");

    internal static void Connecting(ILogger logger) => _connecting(logger, null);
    internal static void Connected(ILogger logger) => _connected(logger, null);

    internal static void OutboundSendStart(ILogger logger, int length) => _outboundSendStart(logger, length, null);
    internal static void OutboundSendSucceeded(ILogger logger) => _outboundSendSucceeded(logger, null);
    internal static void OutboundOperationCanceled(ILogger logger) => _outboundOperationCanceled(logger, null);
    internal static void OutboundSendFailed(ILogger logger, Exception error) => _outboundSendFailed(logger, error);

    internal static void InboundUserLineReceived(ILogger logger, string sender, int length) => _inboundUserLineReceived(logger, sender, length, null);
    internal static void InboundEmptySkipped(ILogger logger) => _inboundEmptySkipped(logger, null);
    internal static void InboundAppendedToJournal(ILogger logger, string entryType, long position) => _inboundAppendedToJournal(logger, entryType, position, null);

    internal static void ConsoleToChatObserved(ILogger logger, string entryType, long position) => _consoleToChatObserved(logger, entryType, position, null);
    internal static void ConsoleToChatTransmuted(ILogger logger, string fromType, string toType) => _consoleToChatTransmuted(logger, fromType, toType, null);
    internal static void ConsoleToChatAppended(ILogger logger, string entryType, long position) => _consoleToChatAppended(logger, entryType, position, null);

    internal static void ChatToConsoleObserved(ILogger logger, string entryType, long position) => _chatToConsoleObserved(logger, entryType, position, null);
    internal static void ChatToConsoleTransmuted(ILogger logger, string fromType, string toType) => _chatToConsoleTransmuted(logger, fromType, toType, null);
    internal static void ChatToConsoleAppended(ILogger logger, string entryType, long position) => _chatToConsoleAppended(logger, entryType, position, null);

    internal static void ConsoleScrivenerAppended(ILogger logger, string entryType, long position) => _consoleScrivenerAppended(logger, entryType, position, null);
}

