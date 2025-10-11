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

    // Inbound receive and journal append
    private static readonly Action<ILogger, string, int, Exception?> _inboundUserMessageReceived =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2020, nameof(InboundUserMessageReceived)),
            "Inbound Discord message received from {Sender} (length {Length}).");

    private static readonly Action<ILogger, string, int, Exception?> _inboundBotMessageObserved =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(2021, nameof(InboundBotMessageObserved)),
            "Observed bot-authored message from {Sender} (length {Length}); recording ACK.");

    private static readonly Action<ILogger, string, long, Exception?> _inboundAppendedToJournal =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2022, nameof(InboundAppendedToJournal)),
            "Appended inbound {EntryType} to Discord journal at position {Position}.");

    // Pump: Discord -> Chat
    private static readonly Action<ILogger, string, long, Exception?> _discordToChatObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2030, nameof(DiscordToChatObserved)),
            "Discord→Chat observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _discordToChatTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2031, nameof(DiscordToChatTransmuted)),
            "Discord→Chat transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _discordToChatAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2032, nameof(DiscordToChatAppended)),
            "Discord→Chat appended {EntryType} to Chat journal at position {Position}.");

    // Pump: Chat -> Discord
    private static readonly Action<ILogger, string, long, Exception?> _chatToDiscordObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2040, nameof(ChatToDiscordObserved)),
            "Chat→Discord observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _chatToDiscordTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2041, nameof(ChatToDiscordTransmuted)),
            "Chat→Discord transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _chatToDiscordAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2042, nameof(ChatToDiscordAppended)),
            "Chat→Discord appended {EntryType} to Discord journal at position {Position}.");

    // DiscordScrivener internal append
    private static readonly Action<ILogger, string, long, Exception?> _discordScrivenerAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2050, nameof(DiscordScrivenerAppended)),
            "DiscordScrivener appended {EntryType} to internal journal at position {Position}.");

    internal static void Connecting(ILogger logger, ulong channelId) =>
        _connecting(logger, channelId, null);

    internal static void Connected(ILogger logger) =>
        _connected(logger, null);

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

    internal static void InboundUserMessageReceived(ILogger logger, string sender, int length) =>
        _inboundUserMessageReceived(logger, sender, length, null);

    internal static void InboundBotMessageObserved(ILogger logger, string sender, int length) =>
        _inboundBotMessageObserved(logger, sender, length, null);

    internal static void InboundAppendedToJournal(ILogger logger, string entryType, long position) =>
        _inboundAppendedToJournal(logger, entryType, position, null);

    internal static void DiscordToChatObserved(ILogger logger, string entryType, long position) =>
        _discordToChatObserved(logger, entryType, position, null);

    internal static void DiscordToChatTransmuted(ILogger logger, string fromType, string toType) =>
        _discordToChatTransmuted(logger, fromType, toType, null);

    internal static void DiscordToChatAppended(ILogger logger, string entryType, long position) =>
        _discordToChatAppended(logger, entryType, position, null);

    internal static void ChatToDiscordObserved(ILogger logger, string entryType, long position) =>
        _chatToDiscordObserved(logger, entryType, position, null);

    internal static void ChatToDiscordTransmuted(ILogger logger, string fromType, string toType) =>
        _chatToDiscordTransmuted(logger, fromType, toType, null);

    internal static void ChatToDiscordAppended(ILogger logger, string entryType, long position) =>
        _chatToDiscordAppended(logger, entryType, position, null);

    internal static void DiscordScrivenerAppended(ILogger logger, string entryType, long position) =>
        _discordScrivenerAppended(logger, entryType, position, null);
}
