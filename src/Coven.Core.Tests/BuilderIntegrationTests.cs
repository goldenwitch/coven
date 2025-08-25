using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class BuilderIntegrationTests
{
    private sealed class StringLengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    [Fact]
    public async Task Builder_Done_ReturnsCoven_ThatExecutesPrecompiledPipeline()
    {
        var coven = new MagikBuilder<string, int>()
            .MagikBlock(new StringLengthBlock())
            .Done();

        var result = await coven.Ritual<string, int>("hello");
        Assert.Equal(5, result);
    }
}

