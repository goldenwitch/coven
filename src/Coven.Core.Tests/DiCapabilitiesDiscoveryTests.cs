// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;
using Xunit;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;

namespace Coven.Core.Tests;

public sealed class DiCapabilitiesDiscoveryTests
{
    [Fact]
    public async Task AttributeBased_Capabilities_Are_Respected_In_DI()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double, CapFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleAdd(1000));
            c.Done();
        });
        var result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result); // routes to A due to fast
    }

    [Fact]
    public async Task Paramless_ITagCapabilities_Are_Respected_In_DI()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double, CapParamlessFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleAdd(1000));
            c.Done();
        });
        var result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(2004d, result); // routes to C due to fast
    }

    [Fact]
    public async Task CapabilityTags_Are_Merged_From_Builder_Attribute_And_Interface()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, EmitMany>();
            c.AddBlock<int, double, CapMerged>(capabilities: new[] { "ai" });
            c.AddBlock<int, double>(sp => new IntToDoubleAdd(1000));
            c.Done();
        });
        var result = await host.Coven.Ritual<string, double>("abcd");
        // MergedCaps should win with 3 matches: fast (attr) + gpu (interface) + ai (builder)
        Assert.Equal(3004d, result);
    }
}
