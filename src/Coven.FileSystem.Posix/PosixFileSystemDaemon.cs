// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;
using Microsoft.Extensions.Logging;

namespace Coven.FileSystem.Posix;

/// <summary>
/// POSIX leaf daemon that tails the FileSystem journal and processes efferent entries via System.IO.
/// </summary>
internal sealed partial class PosixFileSystemDaemon(
    IScrivener<DaemonEvent> scrivener,
    IScrivener<FileSystemEntry> journal,
    PosixFileSystemConfig config,
    ILogger<PosixFileSystemDaemon> logger) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly IScrivener<FileSystemEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly PosixFileSystemConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private CancellationTokenSource? _cts;
    private Task? _processTask;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processTask = ProcessEntries(_cts.Token);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_processTask is not null)
        {
            try
            {
                await _processTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Status != Status.Completed)
            {
                await Shutdown(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async Task ProcessEntries(CancellationToken ct)
    {
        try
        {
            await foreach ((long _, FileSystemEntry entry) in _journal.TailAsync(0, ct).ConfigureAwait(false))
            {
                if (entry is FileRead read)
                {
                    await HandleFileRead(read, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Fail(ex, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task HandleFileRead(FileRead read, CancellationToken ct)
    {
        try
        {
            string fullPath = ResolvePath(read.Path);
            LogReadingFile(_logger, fullPath);

            if (!File.Exists(fullPath))
            {
                await _journal.WriteAsync(
                    new FileFailure(read.CorrelationId, "NotFound", $"File not found: {read.Path}"), ct).ConfigureAwait(false);
                return;
            }

            string content = await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
            await _journal.WriteAsync(
                new FileContent(read.CorrelationId, content), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogReadFailed(_logger, ex, read.Path);
            await _journal.WriteAsync(
                new FileFailure(read.CorrelationId, "ReadFailed", ex.Message), ct).ConfigureAwait(false);
        }
    }

    private string ResolvePath(string path)
    {
        string resolved = Path.GetFullPath(Path.Combine(_config.Root, path));

        string normalizedRoot = Path.GetFullPath(_config.Root);
        return resolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? resolved
            : throw new UnauthorizedAccessException(
                $"Path '{path}' resolves outside the configured root '{normalizedRoot}'.");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "PosixFS reading: {Path}")]
    private static partial void LogReadingFile(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "PosixFS read failed for {Path}")]
    private static partial void LogReadFailed(ILogger logger, Exception exception, string path);
}
