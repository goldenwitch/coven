using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;
using Xunit;
using Coven.Core.Di;

namespace Coven.Core.Tests;

public sealed class DiCapabilitiesDiscoveryTests
{
    private sealed class EmitFast : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input)
        {
            Tag.Add("fast");
            return Task.FromResult(input.Length);
        }
    }

    [TagCapabilities("fast")]
    private sealed class A : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int i) => Task.FromResult((double)i);
    }

    private sealed class B : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int i) => Task.FromResult((double)i + 1000d);
    }

    private sealed class CWithParamless : IMagikBlock<int, double>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "fast" };
        public Task<double> DoMagik(int i) => Task.FromResult((double)i + 2000d);
    }

    private sealed class EmitMany : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input)
        {
            Tag.Add("fast");
            Tag.Add("gpu");
            Tag.Add("ai");
            return Task.FromResult(input.Length);
        }
    }

    [TagCapabilities("fast")] // attribute
    private sealed class MergedCaps : IMagikBlock<int, double>, ITagCapabilities // paramless interface
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "gpu" };
        public Task<double> DoMagik(int i) => Task.FromResult((double)i + 3000d);
    }

    [Fact]
    public async Task AttributeBased_Capabilities_Are_Respected_In_DI()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double, A>(); // attribute declares fast
            c.AddBlock<int, double, B>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(4d, result); // routes to A due to fast
    }

    [Fact]
    public async Task Paramless_ITagCapabilities_Are_Respected_In_DI()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double, CWithParamless>(); // paramless ITagCapabilities advertises fast
            c.AddBlock<int, double, B>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(2004d, result); // routes to C due to fast
    }

    [Fact]
    public async Task CapabilityTags_Are_Merged_From_Builder_Attribute_And_Interface()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitMany>();
            c.AddBlock<int, double, MergedCaps>(capabilities: new[] { "ai" }); // builder adds ai
            c.AddBlock<int, double, B>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");
        // MergedCaps should win with 3 matches: fast (attr) + gpu (interface) + ai (builder)
        Assert.Equal(3004d, result);
    }
}
