using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class PullCompletionAssessorTests
{
    private sealed class AppendRan : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input) => Task.FromResult(input + "|ran");
    }

    [Fact]
    public async Task Pull_IsInitialComplete_True_Completes_Without_Steps()
    {
        var options = new PullOptions { ShouldComplete = _ => true };

        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new AppendRan())
            .Done(pull: true, pullOptions: options);

        var result = await coven.Ritual<string, string>("hello");
        Assert.Equal("hello", result); // no step executed
    }

    [Fact]
    public async Task Pull_IsInitialComplete_False_Forces_At_Least_One_Step()
    {
        // Complete only after at least one step (when output contains the marker)
        var options = new PullOptions { ShouldComplete = o => o is string s && s.Contains("|ran") };

        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new AppendRan())
            .Done(pull: true, pullOptions: options);

        var result = await coven.Ritual<string, string>("hello");
        Assert.Equal("hello|ran", result); // step executed
    }
}
