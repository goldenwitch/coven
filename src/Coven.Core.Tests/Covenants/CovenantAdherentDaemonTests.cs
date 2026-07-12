// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Covenants;
using Coven.Core.Daemonology;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests.Covenants;

public class CovenantAdherentDaemonTests
{
    private sealed record SourceEntry : Entry;

    private sealed record TargetEntry : Entry;

    [Fact]
    public async Task SynchronousPumpCreationFaultTransitionsDaemonToFailed()
    {
        InMemoryScrivener<DaemonEvent> daemonEvents = new();
        CovenantDescriptor covenant = new(
            Manifests: [],
            Pumps:
            [
                new PumpDescriptor(
                    typeof(SourceEntry),
                    typeof(TargetEntry),
                    (_, _) => throw new InvalidOperationException("boom"))
            ]);

        ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        CovenantAdherentDaemon daemon = new(daemonEvents, covenant, services);

        try
        {
            await daemon.Start();

            Exception ex = await daemon.WaitForFailure();

            Assert.Equal(Status.Failed, daemon.Status);
            Assert.Equal("boom", ex.Message);
        }
        finally
        {
            await services.DisposeAsync();
        }
    }
}