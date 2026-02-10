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
    private static readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _normalizedRoot = NormalizeRoot(config.Root);
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Read a file within the configured sandbox root. Returns a result, never throws for expected failures.
    /// </summary>
    public async Task<FileOperationResult> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            string fullPath = ResolvePath(path);
            PosixFileSystemLog.ReadingFile(_logger, fullPath);

            string content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return new FileOperationResult.Success(content);
        }
        catch (UnauthorizedAccessException ex)
        {
            PosixFileSystemLog.ReadFailed(_logger, ex, path);
            return new FileOperationResult.AccessDenied(path, ex.Message);
        }
        catch (FileNotFoundException)
        {
            return new FileOperationResult.NotFound(path);
        }
        catch (DirectoryNotFoundException)
        {
            return new FileOperationResult.NotFound(path);
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

        // Resolve symlinks to prevent sandbox escape via symbolic links
        if (File.Exists(resolved) || Directory.Exists(resolved))
        {
            FileSystemInfo info = File.Exists(resolved)
                ? new FileInfo(resolved)
                : new DirectoryInfo(resolved);
            FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                resolved = Path.GetFullPath(target.FullName);
            }
        }

        return resolved.StartsWith(_normalizedRoot, _pathComparison)
            || string.Equals(resolved, _normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), _pathComparison)
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
