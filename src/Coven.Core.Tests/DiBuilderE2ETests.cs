using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Di;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class DiBuilderE2ETests
{
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
    public async Task Order_Is_Preserved_And_Pipeline_Works_E2E()
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

        // A should be chosen by registration order tie-breaker: 4 + 1 = 5
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task Routing_Follows_Capabilities_And_Explicit_To()
    {
        var services = new ServiceCollection();

        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleA(), capabilities: new[] { "fast" }); // capability match
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });

        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var out1 = await coven.Ritual<string, double>("abc");
        Assert.Equal(3d + 1d, out1); // A selected via capability

        // Explicit override to B using to:<TypeName>
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
        // Route to B explicitly: 3 + 1000
        Assert.Equal(1003d, out2);
    }

    [Fact]
    public async Task Done_Precompiles_All_Pipelines_No_Lazy_Compiles()
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

        // Reflect pipeline cache count
        var boardType = board.GetType();
        var field = boardType.GetField("pipelineCache", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var cache = (System.Collections.IDictionary)field!.GetValue(board)!;
        var preCount = cache.Count;
        Assert.True(preCount > 0, "Expected precompiled pipelines in cache after Done().");

        // Execute a ritual and ensure no new entries are added (no lazy compiles)
        var coven = sp.GetRequiredService<ICoven>();
        var _ = await coven.Ritual<string, double>("abcd");
        var postCount = ((System.Collections.IDictionary)field.GetValue(board)!).Count;
        Assert.Equal(preCount, postCount);
    }
}
