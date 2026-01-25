// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core;

/// <summary>
/// Exception thrown when daemon startup fails during scope activation.
/// Contains information about which daemon failed and which daemons were rolled back.
/// </summary>
/// <param name="message">The error message.</param>
/// <param name="innerException">The exception that caused the startup failure.</param>
/// <param name="failedDaemon">The daemon type that failed to start.</param>
/// <param name="rolledBackDaemons">Daemon types that were successfully started and then rolled back.</param>
public sealed class DaemonStartupException(
    string message,
    Exception innerException,
    Type failedDaemon,
    IReadOnlyList<Type> rolledBackDaemons)
    : Exception(message, innerException)
{
    /// <summary>The daemon type that failed to start.</summary>
    public Type FailedDaemon { get; } = failedDaemon;

    /// <summary>Daemon types that were successfully started and then rolled back.</summary>
    public IReadOnlyList<Type> RolledBackDaemons { get; } = rolledBackDaemons;
}
