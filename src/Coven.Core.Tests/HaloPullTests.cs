// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class HaloPullTests
{
    // Shared minimal types from the Halo E2E to lock behavior shape
    [Fact]
    public async Task HaloPullEndToEndWithBuilderAndRitual()
    {
        // Build Coven in Pull mode via DI
        PullOptions options = new()

        {
            ShouldComplete = o => o is string s &&
                                     s.Contains("PRAISE THE SUN", StringComparison.OrdinalIgnoreCase) &&
                                     s.Equals(s, StringComparison.OrdinalIgnoreCase)
        };
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.MagikBlock<string, Doc, ParseAndTag>()
             .MagikBlock<Doc, Doc, AddSalutation>(capabilities: ["exclaim"])
             .MagikBlock<Doc, Doc, UppercaseText>(capabilities: ["style:loud"])
             .MagikBlock<Doc, string, DocToOut>();
        }, options);

        string input = "hello coven!!! let's test tags";
        string output = await host.Coven.Ritual<string, string>(input);

        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }
}
