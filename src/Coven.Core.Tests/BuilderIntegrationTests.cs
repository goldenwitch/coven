// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderIntegrationTests
{
    private sealed class StringLengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    [Fact]
    public async Task Builder_Done_ReturnsCoven_ThatExecutesPipeline()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringLengthBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);
    }

    // DI-first equivalents of routing and precompilation tests formerly in DiBuilderE2ETests
    private sealed class StringToInt : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    private sealed class IntToDoubleA : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult(input + 1d);
    }

    private sealed class IntToDoubleB : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult(input + 1000d);
    }

    private sealed class EmitFast : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string s)
        {
            Tag.Add("fast");
            return Task.FromResult(s.Length);
        }
    }

    private sealed class EmitToB : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int i)
        {
            Tag.Add($"to:{nameof(IntToDoubleB)}");
            return Task.FromResult(i);
        }
    }

    [Fact]
    public async Task Di_Order_Is_Preserved_And_Pipeline_Works_E2E()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDoubleA>(); // first candidate
            c.AddBlock<int, double, IntToDoubleB>(); // second candidate
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result); // 4 + 1
    }

    [Fact]
    public async Task Di_Routing_Follows_Capabilities_And_Explicit_To()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleA(), capabilities: new[] { "fast" });
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var out1 = await coven.Ritual<string, double>("abc");
        Assert.Equal(3d + 1d, out1);

        var services2 = new ServiceCollection();
        services2.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, int, EmitToB>();
            c.AddBlock<int, double, IntToDoubleA>();
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });
        using var sp2 = services2.BuildServiceProvider();
        var coven2 = sp2.GetRequiredService<ICoven>();
        var out2 = await coven2.Ritual<string, double>("abc");
        Assert.Equal(1003d, out2);
    }

    [Fact]
    public async Task Di_Done_Precompiles_All_Pipelines_No_Lazy_Compiles()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDoubleA>();
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var board = sp.GetRequiredService<IBoard>();

        var boardType = board.GetType();
        var field = boardType.GetField("pipelineCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var cache = (System.Collections.IDictionary)field!.GetValue(board)!;
        var preCount = cache.Count;
        Assert.True(preCount > 0);

        var coven = sp.GetRequiredService<ICoven>();
        var _ = await coven.Ritual<string, double>("abcd");
        var postCount = ((System.Collections.IDictionary)field.GetValue(board)!).Count;
        Assert.Equal(preCount, postCount);
    }
}