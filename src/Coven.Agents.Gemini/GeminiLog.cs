// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Agents.Gemini;

internal static class GeminiLog
{
    private static readonly Action<ILogger, Exception?> _connected =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4001, nameof(Connected)),
            "Gemini session connected.");

    private static readonly Action<ILogger, int, Exception?> _outboundSendStart =
        LoggerMessage.Define<int>(
            LogLevel.Trace,
            new EventId(4006, nameof(OutboundSendStart)),
            "Sending request to Gemini (length {Length}).");

    private static readonly Action<ILogger, Exception?> _outboundSendSucceeded =
        LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(4007, nameof(OutboundSendSucceeded)),
            "Sent request to Gemini.");

    private static readonly Action<ILogger, string, long, Exception?> _geminiScrivenerAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(4050, nameof(GeminiScrivenerAppended)),
            "GeminiScrivener appended {EntryType} to internal journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _geminiToAgentsObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(4030, nameof(GeminiToAgentsObserved)),
            "Gemini→Agents observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _geminiToAgentsTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Trace,
            new EventId(4031, nameof(GeminiToAgentsTransmuted)),
            "Gemini→Agents transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _geminiToAgentsAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(4032, nameof(GeminiToAgentsAppended)),
            "Gemini→Agents appended {EntryType} to Agents journal at position {Position}.");

    private static readonly Action<ILogger, string, long, Exception?> _agentsToGeminiObserved =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(4040, nameof(AgentsToGeminiObserved)),
            "Agents→Gemini observed {EntryType} at position {Position}.");

    private static readonly Action<ILogger, string, string, Exception?> _agentsToGeminiTransmuted =
        LoggerMessage.Define<string, string>(
            LogLevel.Trace,
            new EventId(4041, nameof(AgentsToGeminiTransmuted)),
            "Agents→Gemini transmuted {FromType} → {ToType}.");

    private static readonly Action<ILogger, string, long, Exception?> _agentsToGeminiAppended =
        LoggerMessage.Define<string, long>(
            LogLevel.Trace,
            new EventId(4042, nameof(AgentsToGeminiAppended)),
            "Agents→Gemini appended {EntryType} to Gemini journal at position {Position}.");

    private static readonly Action<ILogger, Exception?> _geminiToAgentsPumpFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4060, nameof(GeminiToAgentsPumpFailed)),
            "Gemini→Agents pump failed.");

    private static readonly Action<ILogger, Exception?> _agentsToGeminiPumpFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4061, nameof(AgentsToGeminiPumpFailed)),
            "Agents→Gemini pump failed.");

    private static readonly Action<ILogger, Exception?> _geminiToAgentsPumpCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4062, nameof(GeminiToAgentsPumpCanceled)),
            "Gemini→Agents pump canceled.");

    private static readonly Action<ILogger, Exception?> _agentsToGeminiPumpCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4063, nameof(AgentsToGeminiPumpCanceled)),
            "Agents→Gemini pump canceled.");

    private static readonly Action<ILogger, Exception?> _geminiToAgentsPumpCompleted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4064, nameof(GeminiToAgentsPumpCompleted)),
            "Gemini→Agents pump completed.");

    private static readonly Action<ILogger, Exception?> _agentsToGeminiPumpCompleted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4065, nameof(AgentsToGeminiPumpCompleted)),
            "Agents→Gemini pump completed.");

    private static readonly Action<ILogger, string, Exception?> _streamLine =
        LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(4075, nameof(StreamLine)),
            "Gemini stream line: {Line}");

    private static readonly Action<ILogger, string, string?, Exception?> _safetyBlocked =
        LoggerMessage.Define<string, string?>(
            LogLevel.Warning,
            new EventId(4070, nameof(SafetyBlocked)),
            "Gemini safety block: reason {Reason}, category {Category}.");

    internal static void Connected(ILogger logger) => _connected(logger, null);
    internal static void OutboundSendStart(ILogger logger, int length) => _outboundSendStart(logger, length, null);
    internal static void OutboundSendSucceeded(ILogger logger) => _outboundSendSucceeded(logger, null);
    internal static void GeminiScrivenerAppended(ILogger logger, string entryType, long position) => _geminiScrivenerAppended(logger, entryType, position, null);
    internal static void GeminiToAgentsObserved(ILogger logger, string entryType, long position) => _geminiToAgentsObserved(logger, entryType, position, null);
    internal static void GeminiToAgentsTransmuted(ILogger logger, string fromType, string toType) => _geminiToAgentsTransmuted(logger, fromType, toType, null);
    internal static void GeminiToAgentsAppended(ILogger logger, string entryType, long position) => _geminiToAgentsAppended(logger, entryType, position, null);
    internal static void AgentsToGeminiObserved(ILogger logger, string entryType, long position) => _agentsToGeminiObserved(logger, entryType, position, null);
    internal static void AgentsToGeminiTransmuted(ILogger logger, string fromType, string toType) => _agentsToGeminiTransmuted(logger, fromType, toType, null);
    internal static void AgentsToGeminiAppended(ILogger logger, string entryType, long position) => _agentsToGeminiAppended(logger, entryType, position, null);
    internal static void GeminiToAgentsPumpFailed(ILogger logger, Exception ex) => _geminiToAgentsPumpFailed(logger, ex);
    internal static void AgentsToGeminiPumpFailed(ILogger logger, Exception ex) => _agentsToGeminiPumpFailed(logger, ex);
    internal static void GeminiToAgentsPumpCanceled(ILogger logger) => _geminiToAgentsPumpCanceled(logger, null);
    internal static void AgentsToGeminiPumpCanceled(ILogger logger) => _agentsToGeminiPumpCanceled(logger, null);
    internal static void GeminiToAgentsPumpCompleted(ILogger logger) => _geminiToAgentsPumpCompleted(logger, null);
    internal static void AgentsToGeminiPumpCompleted(ILogger logger) => _agentsToGeminiPumpCompleted(logger, null);
    internal static void SafetyBlocked(ILogger logger, string reason, string? category) => _safetyBlocked(logger, reason, category, null);
    internal static void StreamLine(ILogger logger, string line) => _streamLine(logger, line, null);
}
