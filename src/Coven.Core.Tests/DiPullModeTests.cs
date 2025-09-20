// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;
using System.Collections.Generic;

namespace Coven.Core.Tests;

public class DiPullModeTests
{
    [Fact]
    public async Task Pull_Mode_Works_With_DI_Blocks()
    {
        using var host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, int, StringLength>();
            c.AddBlock<int, double, IntToDoubleAddOne>();
        });
        var result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task Pull_Mode_Uses_Merged_Capabilities_To_Select_Best_Block()
    {
        using var host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, int, EmitMany>();
            c.AddBlock<int, double>(sp => new IntToDoubleAdd(1000)); // earlier registration
            c.AddBlock<int, double, CapMerged>(capabilities: new[] { "ai" }); // builder + attribute + interface
        });
        var result = await host.Coven.Ritual<string, double>("abcd");

        // CapMerged should win due to 3 matches (fast, gpu, ai), despite later registration
        Assert.Equal(3004d, result);
    }
}
