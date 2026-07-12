// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Daemonology.Tests;

public class ContractDaemonFailureTests
{
    [Fact]
    public async Task WaitForFailurePropagatesFirstException()
    {
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        await daemon.TriggerFailure(new InvalidOperationException("boom"));

        Exception ex = await daemon.WaitForFailure();

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task FailTransitionsDaemonToFailed()
    {
        InMemoryScrivener<DaemonEvent> scrivener = new();
        TestDaemon daemon = new(scrivener);

        Task waitForFailed = daemon.WaitFor(Status.Failed);

        await daemon.TriggerFailure(new InvalidOperationException("boom"));
        await waitForFailed;

        Assert.Equal(Status.Failed, daemon.Status);
    }
}
