// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// High-performance logging for the LLamaSharp agent integration.
/// </summary>
internal static partial class LLamaSharpLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "LLamaSharp loading model from {ModelPath}")]
    public static partial void ModelLoading(ILogger logger, string modelPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLamaSharp model loaded in {ElapsedMs}ms")]
    public static partial void ModelLoaded(ILogger logger, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLamaSharp gateway connected")]
    public static partial void Connected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLamaSharp outbound send starting")]
    public static partial void OutboundSendStart(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLamaSharp outbound send succeeded")]
    public static partial void OutboundSendSucceeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "LLamaSharp stream token: {Token}")]
    public static partial void StreamToken(ILogger logger, string token);

    // LLamaSharp → Agents pump
    [LoggerMessage(Level = LogLevel.Trace, Message = "LLamaSharp→Agents observed {EntryType} at position {Position}")]
    public static partial void LLamaSharpToAgentsObserved(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Trace, Message = "LLamaSharp→Agents transmuted {SourceType} to {TargetType}")]
    public static partial void LLamaSharpToAgentsTransmuted(ILogger logger, string sourceType, string targetType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "LLamaSharp→Agents appended {EntryType} at position {Position}")]
    public static partial void LLamaSharpToAgentsAppended(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLamaSharp→Agents pump completed")]
    public static partial void LLamaSharpToAgentsPumpCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLamaSharp→Agents pump canceled")]
    public static partial void LLamaSharpToAgentsPumpCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLamaSharp→Agents pump failed")]
    public static partial void LLamaSharpToAgentsPumpFailed(ILogger logger, Exception exception);

    // Agents → LLamaSharp pump
    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→LLamaSharp observed {EntryType} at position {Position}")]
    public static partial void AgentsToLLamaSharpObserved(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→LLamaSharp transmuted {SourceType} to {TargetType}")]
    public static partial void AgentsToLLamaSharpTransmuted(ILogger logger, string sourceType, string targetType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→LLamaSharp appended {EntryType} at position {Position}")]
    public static partial void AgentsToLLamaSharpAppended(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agents→LLamaSharp pump completed")]
    public static partial void AgentsToLLamaSharpPumpCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agents→LLamaSharp pump canceled")]
    public static partial void AgentsToLLamaSharpPumpCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Agents→LLamaSharp pump failed")]
    public static partial void AgentsToLLamaSharpPumpFailed(ILogger logger, Exception exception);

    // Scrivener
    [LoggerMessage(Level = LogLevel.Trace, Message = "LLamaSharp scrivener appended {EntryType} at position {Position}")]
    public static partial void LLamaSharpScrivenerAppended(ILogger logger, string entryType, long position);
}
