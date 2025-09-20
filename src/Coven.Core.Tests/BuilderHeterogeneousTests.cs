// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderHeterogeneousTests
{
    [Fact]
    public async Task BuilderRegistersHeterogeneousBlocksAndExecutesChain()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.AddBlock<string, int, StringLengthBlock>()
                .AddBlock<int, double, IntToDoubleBlock>()
                .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result);
    }

    [Fact]
    public async Task BuilderRegistersHeterogeneousFuncsAndExecutesChain()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.AddLambda<string, int>((s, ct) => Task.FromResult(s.Length))
                .AddLambda<int, double>((i, ct) => Task.FromResult(i + 0.5))
                .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abc");
        Assert.Equal(3.5d, result);
    }
}
