// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.Scriveners.FileScrivener;

internal static class FlusherLog
{
    private static readonly Action<ILogger, Exception?> _producerCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4000, nameof(ProducerCanceled)),
            "Flusher producer canceled.");

    private static readonly Action<ILogger, Exception?> _producerFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4001, nameof(ProducerFailed)),
            "Flusher producer failed.");

    private static readonly Action<ILogger, Exception?> _consumerCanceled =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4002, nameof(ConsumerCanceled)),
            "Flusher consumer canceled.");

    private static readonly Action<ILogger, Exception?> _consumerFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4003, nameof(ConsumerFailed)),
            "Flusher consumer failed.");

    internal static void ProducerCanceled(ILogger logger) => _producerCanceled(logger, null);
    internal static void ProducerFailed(ILogger logger, Exception ex) => _producerFailed(logger, ex);
    internal static void ConsumerCanceled(ILogger logger) => _consumerCanceled(logger, null);
    internal static void ConsumerFailed(ILogger logger, Exception ex) => _consumerFailed(logger, ex);

    // Info breadcrumbs for normal operation
    private static readonly Action<ILogger, int, Exception?> _snapshotFlushTriggered =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4010, nameof(SnapshotFlushTriggered)),
            "Flusher predicate met; capturing snapshot of {Count} entries.");

    private static readonly Action<ILogger, int, Exception?> _snapshotEnqueued =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(4011, nameof(SnapshotEnqueued)),
            "Flusher enqueued snapshot of {Count} entries.");

    private static readonly Action<ILogger, int, Exception?> _snapshotAppended =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4012, nameof(SnapshotAppended)),
            "Flusher appended snapshot of {Count} entries.");

    private static readonly Action<ILogger, int, Exception?> _shutdownRemainderEnqueued =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4013, nameof(ShutdownRemainderEnqueued)),
            "Flusher shutdown: enqueued remainder of {Count} entries.");

    internal static void SnapshotFlushTriggered(ILogger logger, int count) => _snapshotFlushTriggered(logger, count, null);
    internal static void SnapshotEnqueued(ILogger logger, int count) => _snapshotEnqueued(logger, count, null);
    internal static void SnapshotAppended(ILogger logger, int count) => _snapshotAppended(logger, count, null);
    internal static void ShutdownRemainderEnqueued(ILogger logger, int count) => _shutdownRemainderEnqueued(logger, count, null);
}
