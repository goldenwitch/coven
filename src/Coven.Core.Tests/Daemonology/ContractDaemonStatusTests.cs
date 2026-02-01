// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Daemonology.Tests;

public class ContractDaemonStatusTests
{
    #region Basic Lifecycle

    [Fact]
    public async Task WaitForRunningCompletesOnStart()
    {
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.WaitFor(Status.Running);

        Assert.Equal(Status.Running, daemon.Status);
    }

    [Fact]
    public async Task WaitForCompletedCompletesOnShutdown()
    {
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();
        await daemon.WaitFor(Status.Completed);

        Assert.Equal(Status.Completed, daemon.Status);
    }

    #endregion

    #region Invalid Transitions

    [Fact]
    public async Task CompletedDaemonCannotRestart()
    {
        // Intent: Once a daemon completes, it cannot be restarted.
        // This enforces the one-way lifecycle: Stopped → Running → Completed.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();

        await Assert.ThrowsAsync<InvalidOperationException>(() => daemon.Start());
    }

    [Fact]
    public async Task ShutdownWithoutStartThrows()
    {
        // Intent: A daemon must be started before it can be shut down.
        // Stopped → Completed is not a valid transition.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await Assert.ThrowsAsync<InvalidOperationException>(() => daemon.Shutdown());
    }

    #endregion

    #region Idempotent Transitions

    [Fact]
    public async Task StartIsIdempotentWhenRunning()
    {
        // Intent: Calling Start() on an already-running daemon is a no-op.
        // This allows callers to ensure a daemon is running without tracking state.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Start(); // Should not throw

        Assert.Equal(Status.Running, daemon.Status);
    }

    [Fact]
    public async Task ShutdownIsIdempotentWhenCompleted()
    {
        // Intent: Calling Shutdown() on an already-completed daemon is a no-op.
        // This allows callers to ensure cleanup without tracking state.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();
        await daemon.Shutdown(); // Should not throw

        Assert.Equal(Status.Completed, daemon.Status);
    }

    #endregion

    #region Journaling

    [Fact]
    public async Task StartWritesStatusChangeEvent()
    {
        // Intent: State transitions are journaled for observability.
        // External systems can tail the journal to react to daemon lifecycle.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Single(events);
    }

    [Fact]
    public async Task ShutdownWritesStatusChangeEvent()
    {
        // Intent: Shutdown is journaled so observers know the daemon completed.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Equal(2, events.Count); // Start + Shutdown
    }

    [Fact]
    public async Task IdempotentStartDoesNotWriteDuplicateEvent()
    {
        // Intent: Idempotent calls should not pollute the journal.
        // Only actual state transitions produce events.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Start(); // Idempotent
        await daemon.Start(); // Idempotent

        List<DaemonEvent> events = scrivener.ReadAll();
        Assert.Single(events); // Only one StatusChanged(Running)
    }

    #endregion

    #region WaitFor Edge Cases

    [Fact]
    public async Task WaitForRunningCompletesImmediatelyWhenAlreadyRunning()
    {
        // Intent: WaitFor should not block if the daemon is already in the target state.
        // This allows safe "ensure running" patterns.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();

        // Should complete without timeout
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        await daemon.WaitFor(Status.Running, cts.Token);

        Assert.Equal(Status.Running, daemon.Status);
    }

    [Fact]
    public async Task WaitForCompletedCompletesImmediatelyWhenAlreadyCompleted()
    {
        // Intent: WaitFor should not block if the daemon is already in the target state.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();

        // Should complete without timeout
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        await daemon.WaitFor(Status.Completed, cts.Token);

        Assert.Equal(Status.Completed, daemon.Status);
    }

    [Fact]
    public async Task WaitForRespectsCancellation()
    {
        // Intent: Long waits should be cancellable.
        // Callers can abort if the daemon never reaches the expected state.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        // Daemon is Stopped, waiting for Running will block forever without cancellation
        using CancellationTokenSource cts = new();
        Task waitTask = daemon.WaitFor(Status.Running, cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }

    #endregion
}
