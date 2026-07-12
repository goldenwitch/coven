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
        string candidate = Path.GetFullPath(Path.Combine(_normalizedRoot, path));
        string relativePath = Path.GetRelativePath(_normalizedRoot, candidate);

        if (Path.IsPathRooted(relativePath) || EscapesRoot(relativePath))
        {
            PosixFileSystemLog.PathValidationFailed(_logger, path, candidate, _normalizedRoot);
            throw new UnauthorizedAccessException(
                $"Path '{path}' resolves outside the configured root '{_normalizedRoot}'.");
        }

        string resolved = ResolveSymlinks(relativePath);
        if (IsWithinRoot(resolved))
        {
            return resolved;
        }

        PosixFileSystemLog.PathValidationFailed(_logger, path, resolved, _normalizedRoot);
        throw new UnauthorizedAccessException(
            $"Path '{path}' resolves outside the configured root '{_normalizedRoot}'.");
    }

    private string ResolveSymlinks(string relativePath)
    {
        string current = _normalizedRoot.TrimEnd(Path.DirectorySeparatorChar);
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            current = Path.GetFullPath(Path.Combine(current, segment));

            FileSystemInfo? info = GetExistingPathInfo(current);
            if (info?.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo target)
            {
                current = Path.GetFullPath(target.FullName);
            }
        }

        return current;
    }

    private static FileSystemInfo? GetExistingPathInfo(string path)
        => File.Exists(path)
            ? new FileInfo(path)
            : Directory.Exists(path)
                ? new DirectoryInfo(path)
                : null;

    private static bool EscapesRoot(string relativePath)
        => relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private bool IsWithinRoot(string path)
        => path.StartsWith(_normalizedRoot, _pathComparison)
            || string.Equals(path, _normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), _pathComparison);

    private static string NormalizeRoot(string root)
    {
        string normalized = Path.GetFullPath(root);
        return normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + Path.DirectorySeparatorChar;
    }
}
