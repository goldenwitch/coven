// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class SelectionPreferenceTests
{
    private int blockSteps;
    internal sealed class StringAppendBlock(Action blockStepIncrementer) : IMagikBlock<string, string>
    {
        private static string Appended = "";

        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            blockStepIncrementer();
            Appended = input + Appended;
            // Emit capability tags when we reach length 2 so the next selection
            // prefers the block advertising both tags.
            if (Appended.Length == 2)
            {
                Tag.Add("t1");
                Tag.Add("t2");
            }
            return Task.FromResult(Appended);
        }
    }

    [Fact]
    public async Task SelectionPrefersMoreTotalEvenIfNextTagPointsElsewhere()
    {
        // Use a normal builder. Emit tags favoring the weaker block but include
        // an extra capability tag only the stronger block supports.
        Action incrementer = new(() => blockSteps++);

        PullOptions options = new()

        {
            ShouldComplete = o => o is string s && s.Length >= 4
        };
        using TestHost host = TestBed.BuildPull(c =>
        {
            string appended = "";
            Task<string> step(string input, CancellationToken ct)
            {
                incrementer();
                appended = input + appended;
                if (appended.Length == 2)
                {
                    Tag.Add("t1");
                    Tag.Add("t2");
                }
                return Task.FromResult(appended);
            }
            c.LambdaBlock<string, string>(step)
             .LambdaBlock<string, string>(step)
             .LambdaBlock<string, string>(step)
             .LambdaBlock<string, string>(step)
             .LambdaBlock<string, string>(step)
             .LambdaBlock<string, string>(step, capabilities: ["t1", "t2"]);
        }, options);

        string result = await host.Coven.Ritual<string, string>("a");
        Assert.Equal(3, blockSteps);
        Assert.Equal("aaaa", result);
    }
}
