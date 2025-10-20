// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Agents.OpenAI;

internal static class OpenAILog
{
    private static readonly Action<ILogger, Exception?> _connected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3001, nameof(Connected)),
            "OpenAI session connected.");

    private static readonly Action<ILogger, int, Exception?> _outboundSendStart =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(3006, nameof(OutboundSendStart)),
            "Sending request to OpenAI (length {Length}).");

    private static readonly Action<ILogger, Exception?> _outboundSendSucceeded =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(3007, nameof(OutboundSendSucceeded)),
            "Sent request to OpenAI.");

    private static readonly Action<ILogger, string, long, Exception?> _openAIScrivenerAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3050, nameof(OpenAIScrivenerAppended)),
            "OpenAIScrivener appended {EntryType} to internal journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _openAIToAgentsObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3030, nameof(OpenAIToAgentsObserved)),
            "OpenAI→Agents observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _openAIToAgentsTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3031, nameof(OpenAIToAgentsTransmuted)),
            "OpenAI→Agents transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _openAIToAgentsAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3032, nameof(OpenAIToAgentsAppended)),
            "OpenAI→Agents appended {EntryType} to Agents journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _agentsToOpenAIObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3040, nameof(AgentsToOpenAIObserved)),
            "Agents→OpenAI observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _agentsToOpenAITransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3041, nameof(AgentsToOpenAITransmuted)),
            "Agents→OpenAI transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _agentsToOpenAIAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(3042, nameof(AgentsToOpenAIAppended)),
            "Agents→OpenAI appended {EntryType} to OpenAI journal at position {Position}.");

    internal static void Connected(ILogger logger) => _connected(logger, null);
    internal static void OutboundSendStart(ILogger logger, int length) => _outboundSendStart(logger, length, null);
    internal static void OutboundSendSucceeded(ILogger logger) => _outboundSendSucceeded(logger, null);
    internal static void OpenAIScrivenerAppended(ILogger logger, string entryType, long position) => _openAIScrivenerAppended(logger, entryType, position, null);
    internal static void OpenAIToAgentsObserved(ILogger logger, string entryType, long position) => _openAIToAgentsObserved(logger, entryType, position, null);
    internal static void OpenAIToAgentsTransmuted(ILogger logger, string fromType, string toType) => _openAIToAgentsTransmuted(logger, fromType, toType, null);
    internal static void OpenAIToAgentsAppended(ILogger logger, string entryType, long position) => _openAIToAgentsAppended(logger, entryType, position, null);
    internal static void AgentsToOpenAIObserved(ILogger logger, string entryType, long position) => _agentsToOpenAIObserved(logger, entryType, position, null);
    internal static void AgentsToOpenAITransmuted(ILogger logger, string fromType, string toType) => _agentsToOpenAITransmuted(logger, fromType, toType, null);
    internal static void AgentsToOpenAIAppended(ILogger logger, string entryType, long position) => _agentsToOpenAIAppended(logger, entryType, position, null);
}

