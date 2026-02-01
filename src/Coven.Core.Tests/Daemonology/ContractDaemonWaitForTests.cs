// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Daemonology.Tests;

public class ContractDaemonWaitForTests
{
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
}
