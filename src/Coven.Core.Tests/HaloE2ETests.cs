// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class HaloE2ETests
{
    [Fact]
    public async Task HaloEndToEndCapabilityRoutingUppercaseSalutation()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.AddBlock<string, Doc, ParseAndTag>()
                .AddBlock<Doc, Doc, AddSalutation>()
                .AddBlock(sp => new UppercaseText(), capabilities: ["style:loud"])
                .AddBlock<Doc, string, DocToOut>()
                .AddBlock<Doc, Doc, LowercaseText>()
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
