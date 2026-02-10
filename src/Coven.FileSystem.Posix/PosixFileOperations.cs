// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.Logging;

namespace Coven.FileSystem.Posix;

/// <summary>
/// Sandboxed POSIX file operations. Owns path resolution, security boundary, and System.IO calls.
/// </summary>
internal sealed class PosixFileOperations(
    PosixFileSystemConfig config,
    ILogger<PosixFileOperations> logger)
{
    private readonly string _normalizedRoot = NormalizeRoot(config.Root);
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Read a file within the configured sandbox root. Returns a result, never throws for expected failures.
    /// </summary>
    public async Task<FileOperationResult> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        string fullPath = ResolvePath(path);
        PosixFileSystemLog.ReadingFile(_logger, fullPath);

        if (!File.Exists(fullPath))
        {
            return new FileOperationResult.NotFound(path);
        }

        try
        {
            string content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return new FileOperationResult.Success(content);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PosixFileSystemLog.ReadFailed(_logger, ex, path);
            return new FileOperationResult.ReadFailed(ex.Message);
        }
    }

    private string ResolvePath(string path)
    {
        string resolved = Path.GetFullPath(Path.Combine(_normalizedRoot, path));

        return resolved.StartsWith(_normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolved, _normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            ? resolved
            : throw new UnauthorizedAccessException(
                $"Path '{path}' resolves outside the configured root '{_normalizedRoot}'.");
    }

    private static string NormalizeRoot(string root)
    {
        string normalized = Path.GetFullPath(root);
        return normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + Path.DirectorySeparatorChar;
    }
}
