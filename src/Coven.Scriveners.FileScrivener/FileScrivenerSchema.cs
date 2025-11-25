// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Scriveners.FileScrivener;

/// <summary>
/// Schema/version constants for FileScrivener on-disk format.
/// </summary>
internal static class FileScrivenerSchema
{
    /// <summary>
    /// A free-form schema version string written to each line.
    /// Change this when making incompatible on-disk format changes.
    /// </summary>
    public const string CurrentVersion = "1";
}
