// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Daemonology;

/// <summary>
/// Minimal contract for long-running services controlled by the host.
/// </summary>
public interface IDaemon
{
    /// <summary>Current daemon status.</summary>
    Status Status { get; }

    /// <summary>Starts the daemon.</summary>
    Task Start(CancellationToken cancellationToken = default);

    /// <summary>Requests cooperative shutdown of the daemon.</summary>
    Task Shutdown(CancellationToken cancellationToken = default);
}
