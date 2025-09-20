// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Core.Logging;

internal static class CoreLog
{
    // EventId ranges: 1000-1099 Ritual, 1100-1199 Pull

    // Ritual events
    private static readonly Action<ILogger, string, string, string, Exception?> _ritualBegin =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1000, nameof(RitualBegin)),
            "Ritual begin rid={RitualId}: {Start}->{Target}");

    private static readonly Action<ILogger, string, string, int, Exception?> _ritualEndSatisfied =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(1001, nameof(RitualEndSatisfied)),
            "Ritual end rid={RitualId}: satisfied target {Target} after {StepCount} steps");

    private static readonly Action<ILogger, string, string, int, string, Exception?> _ritualBlocked =
        LoggerMessage.Define<string, string, int, string>(
            LogLevel.Warning,
            new EventId(1002, nameof(RitualBlocked)),
            "Ritual blocked rid={RitualId}: no next from {Type} after index {Index} to reach {Target}");

    private static readonly Action<ILogger, string, int, string, string, Exception?> _ritualSelect =
        LoggerMessage.Define<string, int, string, string>(
            LogLevel.Information,
            new EventId(1003, nameof(RitualSelect)),
            "Select {Block} idx={Idx} input={InputType} rid={RitualId}");

    private static readonly Action<ILogger, string, string, Exception?> _ritualInvoke =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1004, nameof(RitualInvoke)),
            "Invoke {Block} rid={RitualId}");

    private static readonly Action<ILogger, string, string, string, Exception?> _ritualComplete =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1005, nameof(RitualComplete)),
            "Complete {Block} => {OutputType} rid={RitualId}");

    private static readonly Action<ILogger, string, int, Exception?> _ritualAbortedNull =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(1006, nameof(RitualAbortedNull)),
            "Ritual aborted rid={RitualId}: current value is null before selection at step {Step}");

    private static readonly Action<ILogger, string, string, Exception?> _ritualBlockFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1007, nameof(RitualBlockFailed)),
            "Block {Block} failed rid={RitualId}");

    private static readonly Action<ILogger, string, string, Exception?> _ritualExhausted =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1008, nameof(RitualExhausted)),
            "Ritual exhausted steps without producing {Target} rid={RitualId}");

    // Pull events
    private static readonly Action<ILogger, string, string, string, Exception?> _pullBegin =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1100, nameof(PullBegin)),
            "Pull begin rid={RitualId}: input={InputType} branch={Branch}");

    private static readonly Action<ILogger, string, int, Exception?> _pullSelect =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1101, nameof(PullSelect)),
            "Pull select {Block} idx={Idx}");

    private static readonly Action<ILogger, string, Exception?> _pullDispatched =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1102, nameof(PullDispatched)),
            "Pull dispatched {Block}");

    private static readonly Action<ILogger, string, string, string, Exception?> _pullComplete =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1103, nameof(PullComplete)),
            "Pull complete {Block} => {OutputType} branch={Branch}");

    // Public-facing helpers (internal visibility) to invoke delegates
    internal static void RitualBegin(ILogger logger, string ritualId, string start, string target) =>
        _ritualBegin(logger, ritualId, start, target, null);

    internal static void RitualEndSatisfied(ILogger logger, string ritualId, string target, int stepCount) =>
        _ritualEndSatisfied(logger, ritualId, target, stepCount, null);

    internal static void RitualBlocked(ILogger logger, string ritualId, string type, int lastIndex, string target) =>
        _ritualBlocked(logger, ritualId, type, lastIndex, target, null);

    internal static void RitualSelect(ILogger logger, string block, int idx, string inputType, string ritualId) =>
        _ritualSelect(logger, block, idx, inputType, ritualId, null);

    internal static void RitualInvoke(ILogger logger, string block, string ritualId) =>
        _ritualInvoke(logger, block, ritualId, null);

    internal static void RitualComplete(ILogger logger, string block, string outputType, string ritualId) =>
        _ritualComplete(logger, block, outputType, ritualId, null);

    internal static void RitualAbortedNull(ILogger logger, string ritualId, int step) =>
        _ritualAbortedNull(logger, ritualId, step, null);

    internal static void RitualBlockFailed(ILogger logger, string block, string ritualId, Exception ex) =>
        _ritualBlockFailed(logger, block, ritualId, ex);

    internal static void RitualExhausted(ILogger logger, string target, string ritualId) =>
        _ritualExhausted(logger, target, ritualId, null);

    internal static void PullBegin(ILogger logger, string ritualId, string inputType, string branch) =>
        _pullBegin(logger, ritualId, inputType, branch, null);

    internal static void PullSelect(ILogger logger, string block, int idx) =>
        _pullSelect(logger, block, idx, null);

    internal static void PullDispatched(ILogger logger, string block) =>
        _pullDispatched(logger, block, null);

    internal static void PullComplete(ILogger logger, string block, string outputType, string branch) =>
        _pullComplete(logger, block, outputType, branch, null);
}
