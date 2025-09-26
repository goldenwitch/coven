using Coven.Core;

namespace Coven.Daemonology;

/// <summary>
/// Represents a Daemon that is capable of meeting a "Status contract" such that when status changes, promise are completed.
/// </summary>
/// <param name="scrivener">The IScrivener<DaemonEvent> that the contract Daemon uses to fulfill promises.</param>
public abstract class ContractDaemon(IScrivener<DaemonEvent> scrivener) : IDaemon, IDisposable
{
    private readonly IScrivener<DaemonEvent> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private long current;

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

    public async Task<Task> WaitFor(Status target, CancellationToken cancellationToken = default)
    {
        // Logic should be:
        // When we get this call, grab our semaphore so that status can't change until we register our listener.
        // In this case, we never want to miss a status so we MUST latch.
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            // Read from the latest status and return the Task our _scrivener generates for WaitFor starting at the latest index
            return _scrivener.WaitForAsync<StatusChanged>(current, status => status.NewStatus == target, cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task<Task<Exception>> WaitForFailure(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            Task<(long journalPosition, FailureOccurred entry)> wait = _scrivener.WaitForAsync<FailureOccurred>(current, _ => true, cancellationToken);

            // Inline async lambda: projects the tuple into the Exception
            return wait.ContinueWith(
                async t =>
                {
                    (_, FailureOccurred failure) = await t.ConfigureAwait(false);
                    return failure.Exception;
                },
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
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
            current = await _scrivener.WriteAsync(new StatusChanged(newStatus), cancellationToken);
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
            current = await _scrivener.WriteAsync(new FailureOccurred(error), cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public void Dispose()
    {
        _semaphoreSlim.Dispose();
        GC.SuppressFinalize(this);
    }
}
