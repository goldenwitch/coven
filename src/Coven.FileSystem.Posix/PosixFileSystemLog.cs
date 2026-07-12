// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.FileSystem.Posix;

/// <summary>
/// High-performance logging for the POSIX file system integration.
/// </summary>
internal static partial class PosixFileSystemLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "PosixFS reading: {Path}")]
    public static partial void ReadingFile(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PosixFS rejected path '{InputPath}' resolved to '{ResolvedPath}' outside root '{Root}'")]
    public static partial void PathValidationFailed(ILogger logger, string inputPath, string resolvedPath, string root);

    [LoggerMessage(Level = LogLevel.Error, Message = "PosixFS read failed for {Path}")]
    public static partial void ReadFailed(ILogger logger, Exception exception, string path);
}
