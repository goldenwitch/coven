// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;

namespace Coven.FileSystem.Posix;

/// <summary>
/// Leaf daemon that tails the FileSystem journal and delegates file operations to <see cref="PosixFileOperations"/>.
/// Owns only lifecycle and journal routing — no I/O logic.
/// </summary>
internal sealed class PosixFileSystemDaemon(
    IScrivener<DaemonEvent> scrivener,
    IScrivener<FileSystemEntry> journal,
    PosixFileOperations fileOps) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly IScrivener<FileSystemEntry> _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    private readonly PosixFileOperations _fileOps = fileOps ?? throw new ArgumentNullException(nameof(fileOps));
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
        FileOperationResult result = await _fileOps.ReadFileAsync(read.Path, ct).ConfigureAwait(false);

        FileSystemEntry response = result switch
        {
            FileOperationResult.Success ok => new FileContent(read.CorrelationId, ok.Content),
            FileOperationResult.NotFound nf => new FileFailure(read.CorrelationId, "NotFound", $"File not found: {nf.Path}"),
            FileOperationResult.AccessDenied ad => new FileFailure(read.CorrelationId, "AccessDenied", ad.Message),
            FileOperationResult.ReadFailed rf => new FileFailure(read.CorrelationId, "ReadFailed", rf.Message),
            _ => throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}")
        };

        await _journal.WriteAsync(response, ct).ConfigureAwait(false);
    }
}
