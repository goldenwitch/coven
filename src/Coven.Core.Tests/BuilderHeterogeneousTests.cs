// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderHeterogeneousTests
{
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    [Fact]
    public async Task Builder_Registers_HeterogeneousBlocks_AndExecutesChain()
    {
        // Desired builder call pattern: allow adding blocks with varying TIn/TOut types
        var coven = new MagikBuilder<string, double>()
            .MagikBlock(new StringToInt())
            .MagikBlock(new IntToDouble())
            .Done();

        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result);
    }

    [Fact]
    public async Task Builder_Registers_HeterogeneousFuncs_AndExecutesChain()
    {
        var coven = new MagikBuilder<string, double>()
            .MagikBlock((string s) => Task.FromResult(s.Length))
            .MagikBlock((int i) => Task.FromResult((double)i + 0.5))
            .Done();

        var result = await coven.Ritual<string, double>("abc");
        Assert.Equal(3.5d, result);
    }
}
