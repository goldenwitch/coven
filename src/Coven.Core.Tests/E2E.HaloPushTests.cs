// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class HaloPushTests
{
    [Fact]
    public async Task HaloEndToEndCapabilityRoutingUppercaseSalutation()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, Doc, ParseAndTag>()
                .MagikBlock<Doc, Doc, AddSalutation>(capabilities: ["exclaim"])
                .MagikBlock<Doc, Doc, UppercaseText>(capabilities: ["style:loud"])
                .MagikBlock<Doc, string, DocToOut>()
                .MagikBlock<Doc, Doc, LowercaseText>(capabilities: ["style:quiet"])
                .Done();
        });

        string input = "hello coven!!! let's test tags";
        string output = await host.Coven.Ritual<string, string>(input);

        // Uppercased sun praise and phrase should be present
        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("IF ONLY I COULD BE SO GROSSLY INCANDESCENT", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }

}
