// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilityBuilderTests
{
    [Fact]
    public async Task BuilderAssignsCapabilitiesUsedForRouting()
    {
        // Build: string->int (emits 'fast'), then two int->double candidates.
        // We assign capability 'fast' to A via builder; router should pick A.
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, EmitFastBlock>()
                .MagikBlock<int, double, IntToDoubleBlock>(capabilities: ["fast"])
                .LambdaBlock<int, double>((i, ct) => Task.FromResult(i + 1000d))
                .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abc");
        Assert.Equal(3d, result); // Chooses A due to capability match
    }
}
