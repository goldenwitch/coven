// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class CapabilitiesSanityTests
{
    private int blockSteps = 0;
    internal sealed class StringAppendBlock : IMagikBlock<string, string>
    {
        private static string Appended = "";
        private readonly Action blockStepIncrementer;

        public StringAppendBlock(Action blockStepIncrementer)
        {
            this.blockStepIncrementer = blockStepIncrementer;
        }

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
    public async Task Selection_Prefers_More_Total_Even_If_NextTag_Points_Elsewhere()
    {
        // Use a normal builder. Emit tags favoring the weaker block but include
        // an extra capability tag only the stronger block supports.
        var incrementer = new Action(() => blockSteps++);

        var options = new PullOptions
        {
            ShouldComplete = o => o is string s && s.Length >= 4
        };
        using var host = TestBed.BuildPull(c =>
        {
            c.AddBlock(sp => new StringAppendBlock(incrementer));
            c.AddBlock(sp => new StringAppendBlock(incrementer));
            c.AddBlock(sp => new StringAppendBlock(incrementer));
            c.AddBlock(sp => new StringAppendBlock(incrementer));
            c.AddBlock(sp => new StringAppendBlock(incrementer));
            c.AddBlock(sp => new StringAppendBlock(incrementer), capabilities: new[] { "t1", "t2" });
        }, options);

        string result = await host.Coven.Ritual<string, string>("a");
        Assert.Equal(3, blockSteps);
        Assert.Equal("aaaa", result);
    }
}
