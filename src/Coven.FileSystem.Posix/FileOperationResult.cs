// SPDX-License-Identifier: BUSL-1.1

namespace Coven.FileSystem.Posix;

/// <summary>
/// Result of a file operation — discriminated by success or failure.
/// </summary>
internal abstract record FileOperationResult
{
    private FileOperationResult() { }

    internal sealed record Success(string Content) : FileOperationResult;
    internal sealed record NotFound(string Path) : FileOperationResult;
    internal sealed record AccessDenied(string Path, string Message) : FileOperationResult;
    internal sealed record ReadFailed(string Message) : FileOperationResult;
}
