// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class DiPullModeTests
{
    [Fact]
    public async Task PullModeWorksWithDIBlocks()
    {
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, int, StringLengthBlock>()
             .AddBlock<int, double, IntToDoubleAddOneBlock>();
        });
        Board board = Assert.IsType<Board>(host.Services.GetRequiredService<IBoard>());
        int pre = board.Status.CompiledPipelinesCount;
        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
        Assert.Equal(pre, board.Status.CompiledPipelinesCount);
    }

    [Fact]
    public async Task PullModeUsesMergedCapabilitiesToSelectBestBlock()
    {
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, int, EmitManyBlock>()
             .AddBlock(sp => new IntToDoubleAddBlock(1000)) // earlier registration
             .AddBlock<int, double, CapMergedBlock>(capabilities: ["ai"]); // builder + attribute + interface
        });
        double result = await host.Coven.Ritual<string, double>("abcd");

        // CapMerged should win due to 3 matches (fast, gpu, ai), despite later registration
        Assert.Equal(3004d, result);
    }
}
