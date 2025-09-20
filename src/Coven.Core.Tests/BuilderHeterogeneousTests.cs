// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderHeterogeneousTests
{
    [Fact]
    public async Task Builder_Registers_HeterogeneousBlocks_AndExecutesChain()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, StringLength>();
            c.AddBlock<int, double, IntToDouble>();
            c.Done();
        });

        var result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result);
    }

    [Fact]
    public async Task Builder_Registers_HeterogeneousFuncs_AndExecutesChain()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddLambda<string, int>((s, ct) => Task.FromResult(s.Length));
            c.AddLambda<int, double>((i, ct) => Task.FromResult((double)i + 0.5));
            c.Done();
        });

        var result = await host.Coven.Ritual<string, double>("abc");
        Assert.Equal(3.5d, result);
    }
}
