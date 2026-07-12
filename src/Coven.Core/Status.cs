// SPDX-License-Identifier: BUSL-1.1

namespace Coven;

/// <summary>
/// Lifecycle states for daemons.
/// </summary>
public enum Status
{
    /// <summary>Not yet started or fully stopped.</summary>
    Stopped,
    /// <summary>Actively running.</summary>
    Running,
    /// <summary>Completed successfully and cannot be restarted.</summary>
    Completed,
    /// <summary>Encountered a fatal error and cannot continue until shut down.</summary>
    Failed
}
