// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderHeterogeneousTests
{
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    [Fact]
    public async Task Builder_Registers_HeterogeneousBlocks_AndExecutesChain()
    {
        // Desired builder call pattern: allow adding blocks with varying TIn/TOut types
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDouble>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result);
    }

    [Fact]
    public async Task Builder_Registers_HeterogeneousFuncs_AndExecutesChain()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddLambda<string, int>((s, ct) => Task.FromResult(s.Length));
            c.AddLambda<int, double>((i, ct) => Task.FromResult((double)i + 0.5));
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abc");
        Assert.Equal(3.5d, result);
    }
}
