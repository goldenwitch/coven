// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Agents.Claude;

/// <summary>
/// High-performance logging for the Claude agent integration.
/// </summary>
internal static partial class ClaudeLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Claude gateway connected")]
    public static partial void Connected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Claude outbound send starting with {MessageCount} messages")]
    public static partial void OutboundSendStart(ILogger logger, int messageCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Claude outbound send succeeded")]
    public static partial void OutboundSendSucceeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Claude stream line: {Line}")]
    public static partial void StreamLine(ILogger logger, string line);

    // Claude → Agents pump
    [LoggerMessage(Level = LogLevel.Trace, Message = "Claude→Agents observed {EntryType} at position {Position}")]
    public static partial void ClaudeToAgentsObserved(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Claude→Agents transmuted {SourceType} to {TargetType}")]
    public static partial void ClaudeToAgentsTransmuted(ILogger logger, string sourceType, string targetType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Claude→Agents appended {EntryType} at position {Position}")]
    public static partial void ClaudeToAgentsAppended(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Claude→Agents pump completed")]
    public static partial void ClaudeToAgentsPumpCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Claude→Agents pump canceled")]
    public static partial void ClaudeToAgentsPumpCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Claude→Agents pump failed")]
    public static partial void ClaudeToAgentsPumpFailed(ILogger logger, Exception exception);

    // Agents → Claude pump
    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→Claude observed {EntryType} at position {Position}")]
    public static partial void AgentsToClaudeObserved(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→Claude transmuted {SourceType} to {TargetType}")]
    public static partial void AgentsToClaudeTransmuted(ILogger logger, string sourceType, string targetType);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Agents→Claude appended {EntryType} at position {Position}")]
    public static partial void AgentsToClaudeAppended(ILogger logger, string entryType, long position);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agents→Claude pump completed")]
    public static partial void AgentsToClaudePumpCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Agents→Claude pump canceled")]
    public static partial void AgentsToClaudePumpCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Agents→Claude pump failed")]
    public static partial void AgentsToClaudePumpFailed(ILogger logger, Exception exception);

    // Scrivener
    [LoggerMessage(Level = LogLevel.Trace, Message = "Claude scrivener appended {EntryType} at position {Position}")]
    public static partial void ClaudeScrivenerAppended(ILogger logger, string entryType, long position);
}
