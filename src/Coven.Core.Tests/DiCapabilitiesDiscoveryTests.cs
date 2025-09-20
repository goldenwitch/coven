// SPDX-License-Identifier: BUSL-1.1

using Xunit;
using Coven.Core.Tests.Infrastructure;

namespace Coven.Core.Tests;

public sealed class DiCapabilitiesDiscoveryTests
{
    [Fact]
    public async Task AttributeBasedCapabilitiesAreRespectedInDI()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, EmitFastBlock>()
                .MagikBlock<int, double, CapFastBlock>()
                .LambdaBlock<int, double>((i, ct) => Task.FromResult(i + 1000d))
                .Done();
        });
        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result); // routes to A due to fast
    }

    [Fact]
    public async Task ExplicitBuilderCapabilitiesAreRespectedInDI()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, EmitFastBlock>()
                .MagikBlock<int, double, CapParamlessFastBlock>(capabilities: ["fast"])
                .LambdaBlock<int, double>((i, ct) => Task.FromResult(i + 1000d))
                .Done();
        });
        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(2004d, result); // routes to C due to explicit fast
    }

    [Fact]
    public async Task CapabilityTagsAreMergedFromBuilderAttributeAndInterface()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, EmitManyBlock>()
                .MagikBlock<int, double, CapMergedBlock>(capabilities: ["ai"])
                .LambdaBlock<int, double>((i, ct) => Task.FromResult(i + 1000d))
                .Done();
        });
        double result = await host.Coven.Ritual<string, double>("abcd");
        // MergedCaps should win via attr+builder: fast (attr) + ai (builder)
        Assert.Equal(3004d, result);
    }
}
