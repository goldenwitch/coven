// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class HaloPullTests
{
    // Shared minimal types from the Halo E2E to lock behavior shape
    [Fact]
    public async Task Halo_Pull_EndToEnd_WithBuilderAndRitual()
    {
        // Build Coven in Pull mode via DI
        var options = new PullOptions
        {
            ShouldComplete = o => o is string s &&
                                     s.Contains("PRAISE THE SUN", StringComparison.OrdinalIgnoreCase) &&
                                     s == s.ToUpperInvariant()
        };
        using var host = TestBed.BuildPull(c =>
        {
            c.AddBlock<string, Doc, ParseAndTag>();
            c.AddBlock<Doc, Doc, AddSalutation>();
            c.AddBlock<Doc, Doc>(sp => new UppercaseText(), capabilities: new[] { "style:loud" });
            c.AddBlock<Doc, string, DocToOut>();
        }, options);

        var input = "hello coven!!! let's test tags";
        var output = await host.Coven.Ritual<string, string>(input);

        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }
}
