// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilityBuilderTests
{
    [Fact]
    public async Task Builder_Assigns_Capabilities_Used_ForRouting()
    {
        // Build: string->int (emits 'fast'), then two int->double candidates.
        // We assign capability 'fast' to A via builder; router should pick A.
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double>(sp => new IntToDouble(), capabilities: new[] { "fast" });
            c.AddBlock<int, double>(sp => new IntToDoubleAdd(1000));
            c.Done();
        });

        var result = await host.Coven.Ritual<string, double>("abc");
        Assert.Equal(3d, result); // Chooses A due to capability match
    }
}
