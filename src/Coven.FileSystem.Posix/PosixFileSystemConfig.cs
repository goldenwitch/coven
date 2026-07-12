// SPDX-License-Identifier: BUSL-1.1

namespace Coven.FileSystem.Posix;

/// <summary>
/// Configuration for the POSIX file system leaf.
/// </summary>
public sealed class PosixFileSystemConfig
{
    /// <summary>Gets or sets the root directory. All paths are resolved relative to this root.</summary>
    public required string Root { get; set; }
}
