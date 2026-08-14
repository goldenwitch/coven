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
    public async Task ShutdownWithoutStartIsANoOp()
    {
        // Intent: Shutting down a daemon that never ran is harmless, not an error.
        //
        // This reverses an earlier rule that threw on Stopped → Completed to enforce
        // "cannot shut down without starting". That rule fired on the one path that matters
        // most: when Start() throws partway, the daemon is left Stopped, and both the scope's
        // rollback and IAsyncDisposable then call Shutdown(). The throw replaced the real
        // startup error — a GGUF that failed to load, a rejected key — with a complaint about
        // the cleanup, which is the last thing anyone needs while diagnosing a failed start.
        //
        // Nothing was being protected: a never-started daemon holds nothing to release.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Shutdown();

        // Still Stopped, not Completed: it never ran, so there is nothing to have completed.
        Assert.Equal(Status.Stopped, daemon.Status);
    }

    [Fact]
    public async Task ShutdownAfterAFailedStartPreservesTheFailure()
    {
        // Intent: cleanup must not overwrite the reason the daemon failed. A daemon that
        // failed during startup stays Failed through Shutdown, so WaitForFailure still
        // reports the cause.
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.TriggerFailure(new InvalidOperationException("model failed to load"));

        Assert.Equal(Status.Failed, daemon.Status);

        await daemon.Shutdown();

        Assert.Equal(Status.Completed, daemon.Status);

        Exception recorded = await daemon.WaitForFailure().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("model failed to load", recorded.Message);
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
