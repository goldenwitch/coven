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
}

