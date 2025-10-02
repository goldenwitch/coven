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

    // Outbound send breadcrumbs
    private static readonly Action<ILogger, ulong, int, Exception?> _outboundSendStart =
        LoggerMessage.Define<ulong, int>(
            LogLevel.Debug,
            new EventId(2006, nameof(OutboundSendStart)),
            "Sending message to channel {ChannelId} (length {Length}).");

    private static readonly Action<ILogger, ulong, Exception?> _outboundSendSucceeded =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2007, nameof(OutboundSendSucceeded)),
            "Sent message to channel {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _outboundOperationCanceled =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(2008, nameof(OutboundOperationCanceled)),
            "Outbound send canceled for channel {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _outboundSendFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2009, nameof(OutboundSendFailed)),
            "Outbound send failed for channel {ChannelId}.");

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

    // Unexpected errors and shutdown/unwind breadcrumbs
    private static readonly Action<ILogger, Exception?> _inboundHandlerUnexpectedError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2020, nameof(InboundHandlerUnexpectedError)),
            "Inbound message handler threw an unexpected exception.");

    private static readonly Action<ILogger, Exception?> _gatewayStopUnwindError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2021, nameof(GatewayStopUnwindError)),
            "Gateway stop during connect-unwind failed.");

    private static readonly Action<ILogger, Exception?> _gatewayLogoutUnwindError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2022, nameof(GatewayLogoutUnwindError)),
            "Gateway logout during connect-unwind failed.");

    private static readonly Action<ILogger, Exception?> _gatewayStopDisposeError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2023, nameof(GatewayStopDisposeError)),
            "Gateway stop during dispose failed.");

    private static readonly Action<ILogger, Exception?> _gatewayLogoutDisposeError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2024, nameof(GatewayLogoutDisposeError)),
            "Gateway logout during dispose failed.");

    private static readonly Action<ILogger, ulong, Exception?> _sessionOpenFailed =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2025, nameof(SessionOpenFailed)),
            "Discord session open failed for channel {ChannelId}.");

    private static readonly Action<ILogger, Exception?> _sessionDisposeFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2030, nameof(SessionDisposeFailed)),
            "Discord session dispose encountered an unexpected error.");

    private static readonly Action<ILogger, Exception?> _sessionReadUnexpectedError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2031, nameof(SessionReadUnexpectedError)),
            "Discord session ReadIncomingAsync threw an unexpected exception.");

    private static readonly Action<ILogger, Exception?> _sessionReadCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2032, nameof(SessionReadCanceled)),
            "Discord session ReadIncomingAsync canceled.");

    private static readonly Action<ILogger, ulong, Exception?> _sessionOpenStart =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2033, nameof(SessionOpenStart)),
            "Opening Discord session for channel {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _sessionOpenSucceeded =
        LoggerMessage.Define<ulong>(
            LogLevel.Debug,
            new EventId(2034, nameof(SessionOpenSucceeded)),
            "Opened Discord session for channel {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _sessionOpenCanceled =
        LoggerMessage.Define<ulong>(
            LogLevel.Information,
            new EventId(2035, nameof(SessionOpenCanceled)),
            "Discord session open canceled for channel {ChannelId}.");

    private static readonly Action<ILogger, ulong, Exception?> _sessionOpenCleanupDisposeError =
        LoggerMessage.Define<ulong>(
            LogLevel.Error,
            new EventId(2036, nameof(SessionOpenCleanupDisposeError)),
            "Discord session open cleanup failed disposing gateway for channel {ChannelId}.");

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

    internal static void OutboundSendStart(ILogger logger, ulong channelId, int length) =>
        _outboundSendStart(logger, channelId, length, null);

    internal static void OutboundSendSucceeded(ILogger logger, ulong channelId) =>
        _outboundSendSucceeded(logger, channelId, null);

    internal static void OutboundOperationCanceled(ILogger logger, ulong channelId) =>
        _outboundOperationCanceled(logger, channelId, null);

    internal static void OutboundSendFailed(ILogger logger, ulong channelId, Exception error) =>
        _outboundSendFailed(logger, channelId, error);

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

    internal static void InboundHandlerUnexpectedError(ILogger logger, Exception error) =>
        _inboundHandlerUnexpectedError(logger, error);

    internal static void GatewayStopUnwindError(ILogger logger, Exception error) =>
        _gatewayStopUnwindError(logger, error);

    internal static void GatewayLogoutUnwindError(ILogger logger, Exception error) =>
        _gatewayLogoutUnwindError(logger, error);

    internal static void GatewayStopDisposeError(ILogger logger, Exception error) =>
        _gatewayStopDisposeError(logger, error);

    internal static void GatewayLogoutDisposeError(ILogger logger, Exception error) =>
        _gatewayLogoutDisposeError(logger, error);

    internal static void SessionOpenFailed(ILogger logger, ulong channelId, Exception error) =>
        _sessionOpenFailed(logger, channelId, error);

    internal static void SessionDisposeFailed(ILogger logger, Exception error) =>
        _sessionDisposeFailed(logger, error);

    internal static void SessionReadUnexpectedError(ILogger logger, Exception error) =>
        _sessionReadUnexpectedError(logger, error);

    internal static void SessionReadCanceled(ILogger logger) =>
        _sessionReadCanceled(logger, null);

    internal static void SessionOpenStart(ILogger logger, ulong channelId) =>
        _sessionOpenStart(logger, channelId, null);

    internal static void SessionOpenSucceeded(ILogger logger, ulong channelId) =>
        _sessionOpenSucceeded(logger, channelId, null);

    internal static void SessionOpenCanceled(ILogger logger, ulong channelId) =>
        _sessionOpenCanceled(logger, channelId, null);

    internal static void SessionOpenCleanupDisposeError(ILogger logger, ulong channelId, Exception error) =>
        _sessionOpenCleanupDisposeError(logger, channelId, error);
}
