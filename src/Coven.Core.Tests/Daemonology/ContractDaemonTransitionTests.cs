// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Daemonology.Tests;

public class ContractDaemonTransitionTests
{
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
}
