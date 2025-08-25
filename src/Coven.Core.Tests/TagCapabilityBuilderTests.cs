using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilityBuilderTests
{
    private sealed class EmitFast : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input)
        {
            Tag.Add("fast");
            return Task.FromResult(input.Length);
        }
    }

    private sealed class IntToDoubleA : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    private sealed class IntToDoubleB : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input + 1000d);
    }

    [Fact]
    public async Task Builder_Assigns_Capabilities_Used_ForRouting()
    {
        // Build: string->int (emits 'fast'), then two int->double candidates.
        // We assign capability 'fast' to A via builder; router should pick A.
        var coven = new MagikBuilder<string, double>()
            .MagikBlock(new EmitFast())
            .MagikBlock<int, double>(new IntToDoubleA(), new[] { "fast" })
            .MagikBlock<int, double>(new IntToDoubleB())
            .Done();

        var result = await coven.Ritual<string, double>("abc");
        Assert.Equal(3d, result); // Chooses A due to capability match
    }
}

