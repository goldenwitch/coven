// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;

namespace Coven.Daemonology;

/// <summary>
/// Represents a Daemon that is capable of meeting a "Status contract" such that when status changes, promise are completed.
/// </summary>
/// <param name="scrivener">The <see cref="IScrivener{DaemonEvent}"/> used by the daemon to fulfill status promises.</param>
public abstract class ContractDaemon(IScrivener<DaemonEvent> scrivener) : IDaemon, IDisposable
{
    private readonly IScrivener<DaemonEvent> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    /// <summary>
    /// Current operational status of the daemon.
    /// </summary>
    public Status Status { get; protected set; }

    /// <summary>
    /// Consumers should leverage Transition() in their start implementation if they want to use the ContractDaemon.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumers should leverage Transition() in their stop implementation if they want to use the ContractDaemon.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task Shutdown(CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces the first occurance of a status change that matches the target.
    /// </summary>
    /// <param name="target">The status to check status changes against.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task WaitFor(Status target, CancellationToken cancellationToken = default)
        => WaitForCore(target, cancellationToken).Unwrap();

    private async Task<Task> WaitForCore(Status target, CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            // Return the waiter Task; caller will await it. We only await the semaphore.
            return _scrivener.WaitForAsync<StatusChanged>(0, status => status.NewStatus == target, cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Produces the first occurance of a failure.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Exception> WaitForFailure(CancellationToken cancellationToken = default)
        => WaitForFailureCore(cancellationToken).Unwrap();

    private async Task<Task<Exception>> WaitForFailureCore(CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            Task<(long journalPosition, FailureOccurred entry)> wait = _scrivener.WaitForAsync<FailureOccurred>(0, _ => true, cancellationToken);

            // Project the tuple into the Exception; return the Task to caller.
            return wait.ContinueWith(
                t => t.Result.entry.Exception,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Change the Daemon's working status. Using this method ensures that any promises the daemon made will be kept.
    /// </summary>
    /// <param name="newStatus">The new status to set.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected async Task Transition(Status newStatus, CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        if (Status == Status.Completed)
        {
            throw new InvalidOperationException("A completed Daemon may not restart.");
        }
        try
        {
            Status = newStatus;
            await _scrivener.WriteAsync(new StatusChanged(newStatus), cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Change the Daemon's failure status with a new exception. Call this method in your catch to bubble errors up to consumers.
    /// </summary>
    /// <param name="error">The exception we experienced.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task Fail(Exception error, CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);

        try
        {
            await _scrivener.WriteAsync(new FailureOccurred(error), cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Releases resources held by the daemon.
    /// </summary>
    public void Dispose()
    {
        _semaphoreSlim.Dispose();
        GC.SuppressFinalize(this);
    }
}
