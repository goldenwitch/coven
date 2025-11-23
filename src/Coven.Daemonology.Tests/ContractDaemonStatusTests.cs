// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Coven.Daemonology.Tests.Infrastructure;

namespace Coven.Daemonology.Tests;

public class ContractDaemonStatusTests
{
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

        await daemon.Shutdown();
        await daemon.WaitFor(Status.Completed);

        Assert.Equal(Status.Completed, daemon.Status);
    }

    [Fact]
    public async Task CompletedDaemonCannotRestart()
    {
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.Start();
        await daemon.Shutdown();

        await Assert.ThrowsAsync<InvalidOperationException>(() => daemon.Start());
    }
}
