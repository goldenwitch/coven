// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PullCompletionAssessorTests
{
    private sealed class AppendRan : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input + "|ran");
    }

    [Fact]
    public async Task PullIsInitialCompleteTrueCompletesWithoutSteps()
    {
        PullOptions options = new() { ShouldComplete = _ => true };
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, string, AppendRan>();
        }, options);

        string result = await host.Coven.Ritual<string, string>("hello");
        Assert.Equal("hello", result); // no step executed
    }

    [Fact]
    public async Task PullIsInitialCompleteFalseForcesAtLeastOneStep()
    {
        // Complete only after at least one step (when output contains the marker)
        PullOptions options = new() { ShouldComplete = o => o is string s && s.Contains("|ran") };
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, string, AppendRan>();
        }, options);

        string result = await host.Coven.Ritual<string, string>("hello");
        Assert.Equal("hello|ran", result); // step executed
    }
}
