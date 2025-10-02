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

    // Channel resolution breadcrumbs
    private static readonly Action<ILogger, ulong, Exception?> _channelCacheHit =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2010, nameof(ChannelCacheHit)),
            "Channel cache hit for {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _channelCacheMiss =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2011, nameof(ChannelCacheMiss)),
            "Channel cache miss for {ChannelId}; falling back to REST.");

    private static readonly Action<ILogger, ulong, Exception?> _channelRestFetchStart =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2012, nameof(ChannelRestFetchStart)),
            "Fetching channel {ChannelId} via REST.");

    private static readonly Action<ILogger, ulong, Exception?> _channelRestFetchSuccess =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2013, nameof(ChannelRestFetchSuccess)),
            "Fetched channel {ChannelId} via REST.");

    private static readonly Action<ILogger, ulong, string, Exception?> _channelRestFetchInvalidType =
        LoggerMessage.Define<ulong, string>(
            LogLevel.Warning,
            new EventId(2014, nameof(ChannelRestFetchInvalidType)),
            "Fetched channel {ChannelId} but it is not a message channel (actual: {ActualType}).");

    private static readonly Action<ILogger, ulong, Exception?> _channelLookupCanceled =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(2015, nameof(ChannelLookupCanceled)),
            "Channel lookup canceled for {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _channelLookupError =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2016, nameof(ChannelLookupError)),
            "Channel lookup failed for {ChannelId}.");

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

    internal static void ChannelCacheHit(ILogger logger, ulong channelId) =>
        _channelCacheHit(logger, channelId, null);

    internal static void ChannelCacheMiss(ILogger logger, ulong channelId) =>
        _channelCacheMiss(logger, channelId, null);

    internal static void ChannelRestFetchStart(ILogger logger, ulong channelId) =>
        _channelRestFetchStart(logger, channelId, null);

    internal static void ChannelRestFetchSuccess(ILogger logger, ulong channelId) =>
        _channelRestFetchSuccess(logger, channelId, null);

    internal static void ChannelRestFetchInvalidType(ILogger logger, ulong channelId, string actualType) =>
        _channelRestFetchInvalidType(logger, channelId, actualType, null);

    internal static void ChannelLookupCanceled(ILogger logger, ulong channelId) =>
        _channelLookupCanceled(logger, channelId, null);

    internal static void ChannelLookupError(ILogger logger, ulong channelId, Exception error) =>
        _channelLookupError(logger, channelId, error);
}
