// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Chat.Ui;

internal static class UiChatLog
{
    private static readonly Action<ILogger, Exception?> _connected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3400, nameof(Connected)),
            "UI chat session connected.");

    private static readonly Action<ILogger, string, int, Exception?> _inboundReceived =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(3401, nameof(InboundReceived)),
            "UI input received from {Sender} (length {Length}).");

    private static readonly Action<ILogger, string, long, Exception?> _inboundAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Debug,
            new EventId(3402, nameof(InboundAppended)),
            "Appended {EntryType} to UI journal at position {Position}.");

    private static readonly Action<ILogger, string, int, Exception?> _outboundPublished =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(3403, nameof(OutboundPublished)),
            "Published {Kind} to UI channel (length {Length}).");

    private static readonly Action<ILogger, string, long, Exception?> _scrivenerAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(3404, nameof(ScrivenerAppended)),
            "UI scrivener appended {EntryType} at position {Position}.");

    private static readonly Action<ILogger, Exception?> _pumpCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3405, nameof(PumpCanceled)),
            "UI chat pump canceled.");

    private static readonly Action<ILogger, Exception?> _pumpFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3406, nameof(PumpFailed)),
            "UI chat pump failed.");

    internal static void Connected(ILogger logger) => _connected(logger, null);

    internal static void InboundReceived(ILogger logger, string sender, int length) =>
        _inboundReceived(logger, sender, length, null);

    internal static void InboundAppended(ILogger logger, string entryType, long position) =>
        _inboundAppended(logger, entryType, position, null);

    internal static void OutboundPublished(ILogger logger, string kind, int length) =>
        _outboundPublished(logger, kind, length, null);

    internal static void ScrivenerAppended(ILogger logger, string entryType, long position) =>
        _scrivenerAppended(logger, entryType, position, null);

    internal static void PumpCanceled(ILogger logger) => _pumpCanceled(logger, null);

    internal static void PumpFailed(ILogger logger, Exception exception) => _pumpFailed(logger, exception);
}
