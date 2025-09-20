// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

    public class HaloE2ETests
    {
    [Fact]
    public async Task Halo_EndToEnd_CapabilityRouting_UppercaseSalutation()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, Doc, ParseAndTag>();
            c.AddBlock<Doc, Doc, AddSalutation>();
            c.AddBlock<Doc, Doc>(sp => new UppercaseText(), capabilities: new[] { "style:loud" });
            c.AddBlock<Doc, string, DocToOut>();
            c.AddBlock<Doc, Doc, LowercaseText>();
            c.Done();
        });

        var input = "hello coven!!! let's test tags";
        var output = await host.Coven.Ritual<string, string>(input);

        // Uppercased sun praise and phrase should be present
        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("IF ONLY I COULD BE SO GROSSLY INCANDESCENT", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }

}
