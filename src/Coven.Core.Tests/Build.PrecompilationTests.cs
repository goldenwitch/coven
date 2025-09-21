// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderIntegrationTests
{
    

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
