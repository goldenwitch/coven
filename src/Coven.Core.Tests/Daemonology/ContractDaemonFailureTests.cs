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
}
