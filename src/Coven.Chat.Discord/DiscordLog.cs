using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal static class DiscordLog
{
    private static readonly Action<ILogger, ulong, Exception?> _connecting =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(2000, nameof(Connecting)),
            "Discord session connecting to channel {ChannelId}.");

    private static readonly Action<ILogger, Exception?> _connected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2001, nameof(Connected)),
            "Discord session connected.");

    private static readonly Action<ILogger, string, string, Exception?> _inboundMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(2002, nameof(InboundMessage)),
            "Inbound message {MessageId} from {Sender}");

    private static readonly Action<ILogger, Exception?> _disconnected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2003, nameof(Disconnected)),
            "Discord session disconnected.");

    private static readonly Action<ILogger, Exception?> _inboundChannelClosed =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2004, nameof(InboundChannelClosed)),
            "Inbound channel write failed: channel closed (expected during shutdown).");

    private static readonly Action<ILogger, Exception?> _inboundOperationCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2005, nameof(InboundOperationCanceled)),
            "Inbound channel write canceled (expected when cancellation is requested).");

    internal static void Connecting(ILogger logger, ulong channelId) =>
        _connecting(logger, channelId, null);

    internal static void Connected(ILogger logger) =>
        _connected(logger, null);

    internal static void InboundMessage(ILogger logger, string messageId, string sender) =>
        _inboundMessage(logger, messageId, sender, null);

    internal static void Disconnected(ILogger logger) =>
        _disconnected(logger, null);

    internal static void InboundChannelClosed(ILogger logger) =>
        _inboundChannelClosed(logger, null);

    internal static void InboundOperationCanceled(ILogger logger) =>
        _inboundOperationCanceled(logger, null);
}
