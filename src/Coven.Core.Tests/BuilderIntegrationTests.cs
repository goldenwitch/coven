// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderIntegrationTests
{
    [Fact]
    public async Task BuilderDoneReturnsCovenThatExecutesPipeline()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, StringLengthBlock>()
                .Done();
        });

        int result = await host.Coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);
    }

    // DI-first equivalents of routing and precompilation tests formerly in DiBuilderE2ETests
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    private sealed class IntToDoubleA : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + 1d);
    }

    private sealed class IntToDoubleB : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + 1000d);
    }

    private sealed class EmitFast : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string s, CancellationToken cancellationToken = default)
        {
            Tag.Add("fast");
            return Task.FromResult(s.Length);
        }
    }


    [Fact]
    public async Task DiOrderIsPreservedAndPipelineWorksE2E()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, StringToInt>()
                .MagikBlock<int, double, IntToDoubleA>()
                .MagikBlock<int, double, IntToDoubleB>()
                .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result); // 4 + 1
    }

    [Fact]
    public async Task DiRoutingFollowsCapabilities()
    {
        using TestHost host1 = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, EmitFast>()
                .MagikBlock<int, double, IntToDoubleA>(capabilities: ["fast"])
                .MagikBlock<int, double, IntToDoubleB>()
                .Done();
        });

        double out1 = await host1.Coven.Ritual<string, double>("abc");
        Assert.Equal(3d + 1d, out1);
    }

    [Fact]
    public async Task DiDonePrecompilesAllPipelinesNoLazyCompiles()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, StringToInt>()
                .MagikBlock<int, double, IntToDoubleA>()
                .MagikBlock<int, double, IntToDoubleB>()
                .Done();
        });
        IBoard iboard = host.Services.GetRequiredService<IBoard>();
        Board board = Assert.IsType<Board>(iboard);
        int preCount = board.Status.CompiledPipelinesCount;
        Assert.True(preCount > 0);

        await host.Coven.Ritual<string, double>("abcd");
        int postCount = board.Status.CompiledPipelinesCount;
        Assert.Equal(preCount, postCount);
    }
}
