// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Generic;

namespace Coven.Core.Tests;

public class DiPullModeTests
{
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i + 1d);
    }

    [Fact]
    public async Task Pull_Mode_Works_With_DI_Blocks()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDouble>();
            c.Done(pull: true); // build pull-mode board
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    private sealed class EmitMany : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            Tag.Add("fast");
            Tag.Add("gpu");
            Tag.Add("ai");
            return Task.FromResult(input.Length);
        }
    }

    private sealed class FallbackB : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i + 1000d);
    }

    [TagCapabilities("fast")] // attribute
    private sealed class CapMerged : IMagikBlock<int, double>, ITagCapabilities // paramless interface
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "gpu" };
        public Task<double> DoMagik(int i, CancellationToken cancellationToken = default) => Task.FromResult((double)i + 3000d);
    }

    [Fact]
    public async Task Pull_Mode_Uses_Merged_Capabilities_To_Select_Best_Block()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitMany>();
            c.AddBlock<int, double, FallbackB>(); // earlier registration
            c.AddBlock<int, double, CapMerged>(capabilities: new[] { "ai" }); // builder + attribute + interface
            c.Done(pull: true);
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");

        // CapMerged should win due to 3 matches (fast, gpu, ai), despite later registration
        Assert.Equal(3004d, result);
    }
}
