// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderIntegrationTests
{
    [Fact]
    public async Task Builder_Done_ReturnsCoven_ThatExecutesPipeline()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, StringLength>();
            c.Done();
        });

        var result = await host.Coven.Ritual<string, int>("hello");
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
    public async Task Di_Order_Is_Preserved_And_Pipeline_Works_E2E()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDoubleA>();
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });

        var result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result); // 4 + 1
    }

    [Fact]
    public async Task Di_Routing_Follows_Capabilities()
    {
        using var host1 = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleA(), capabilities: new[] { "fast" });
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });

        var out1 = await host1.Coven.Ritual<string, double>("abc");
        Assert.Equal(3d + 1d, out1);
    }

    [Fact]
    public async Task Di_Done_Precompiles_All_Pipelines_No_Lazy_Compiles()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, StringToInt>();
            c.AddBlock<int, double, IntToDoubleA>();
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });
        var board = host.Services.GetRequiredService<IBoard>();

        var boardType = board.GetType();
        var field = boardType.GetField("pipelineCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var cache = (System.Collections.IDictionary)field!.GetValue(board)!;
        var preCount = cache.Count;
        Assert.True(preCount > 0);

        var _ = await host.Coven.Ritual<string, double>("abcd");
        var postCount = ((System.Collections.IDictionary)field.GetValue(board)!).Count;
        Assert.Equal(preCount, postCount);
    }
}
