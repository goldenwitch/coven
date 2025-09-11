// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
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

        public Task<string> DoMagik(string input)
        {
            blockStepIncrementer();
            Appended = input + Appended;
            if (Appended.Length == 3)
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

        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new StringAppendBlock(incrementer))
            .MagikBlock(new StringAppendBlock(incrementer))
            .MagikBlock(new StringAppendBlock(incrementer))
            .MagikBlock(new StringAppendBlock(incrementer))
            .MagikBlock(new StringAppendBlock(incrementer))
            .MagikBlock(new StringAppendBlock(incrementer), ["t1", "t2"])
            .Done(true, new PullOptions
            {
                // Only consider the ritual complete upfront if the input already
                // matches the intended final form (length of 4).
                ShouldComplete = o => o is string s && s.Length >= 4
            });

        string result = await coven.Ritual<string, string>("a");
        Assert.Equal(3, blockSteps);
        Assert.Equal("aaaa", result);
    }
}