// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Daemonology;

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
    /// <returns>True if the transition occurred, false if it was a valid no-op (idempotent).</returns>
    /// <exception cref="InvalidOperationException">Thrown for invalid state transitions.</exception>
    protected async Task<bool> Transition(Status newStatus, CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            // Idempotent: already in target state
            if (Status == newStatus)
            {
                return false;
            }

            // A daemon that never reached Running has nothing to complete, so shutting it
            // down is a no-op rather than an error. This is the normal path when Start()
            // throws partway: the scope rolls back and IAsyncDisposable also calls
            // Shutdown(), and throwing here would replace the real startup error — a model
            // that failed to load, a rejected API key — with a complaint about the cleanup.
            if (Status == Status.Stopped && newStatus == Status.Completed)
            {
                return false;
            }

            // Validate transition
            bool isValid = (Status, newStatus) switch
            {
                (Status.Stopped, Status.Running) => true,
                (Status.Running, Status.Completed) => true,
                (Status.Stopped, Status.Failed) => true,
                (Status.Running, Status.Failed) => true,
                (Status.Failed, Status.Completed) => true,
                (Status.Completed, Status.Running) => false, // Cannot restart
                (Status.Failed, Status.Running) => false, // Cannot restart after failure
                _ => false
            };

            if (!isValid)
            {
                throw new InvalidOperationException(
                    $"Invalid daemon state transition: {Status} → {newStatus}");
            }

            Status = newStatus;
            await _scrivener.WriteAsync(new StatusChanged(newStatus), cancellationToken);
            return true;
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
            if (Status is Status.Stopped or Status.Running)
            {
                Status = Status.Failed;
                await _scrivener.WriteAsync(new StatusChanged(Status.Failed), cancellationToken);
            }

            await _scrivener.WriteAsync(new FailureOccurred(error), cancellationToken);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Reports a session's pump fault as a daemon failure.
    /// </summary>
    /// <param name="sessionCompletion">The session's completion task.</param>
    /// <param name="lifetime">
    /// The session's lifetime source. Cancelled when a fault is seen, so the surviving pumps
    /// stop rather than running on behind a session that has already failed.
    /// </param>
    /// <remarks>
    /// Without this the fault is swallowed: the pumps stop, the daemon stays
    /// <see cref="Status.Running"/>, and the caller waits indefinitely on a turn that is
    /// already dead. Cancellation through <paramref name="lifetime"/> is cooperative shutdown
    /// rather than failure, and is ignored.
    /// </remarks>
    protected async Task MonitorSession(Task sessionCompletion, CancellationTokenSource lifetime)
    {
        ArgumentNullException.ThrowIfNull(sessionCompletion);
        ArgumentNullException.ThrowIfNull(lifetime);

        try
        {
            await sessionCompletion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // Cooperative shutdown.
        }
        catch (Exception ex)
        {
            await lifetime.CancelAsync().ConfigureAwait(false);

            // Not the caller's token: the failure must be recorded even though the session's
            // own lifetime was just cancelled above.
            await Fail(ex, CancellationToken.None).ConfigureAwait(false);
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
