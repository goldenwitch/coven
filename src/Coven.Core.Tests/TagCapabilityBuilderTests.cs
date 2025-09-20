// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilityBuilderTests
{
    private sealed class EmitFast : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            Tag.Add("fast");
            return Task.FromResult(input.Length);
        }
    }

    private sealed class IntToDoubleA : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    private sealed class IntToDoubleB : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input + 1000d);
    }

    [Fact]
    public async Task Builder_Assigns_Capabilities_Used_ForRouting()
    {
        // Build: string->int (emits 'fast'), then two int->double candidates.
        // We assign capability 'fast' to A via builder; router should pick A.
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitFast>();
            c.AddBlock<int, double>(sp => new IntToDoubleA(), capabilities: new[] { "fast" });
            c.AddBlock<int, double, IntToDoubleB>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abc");
        Assert.Equal(3d, result); // Chooses A due to capability match
    }
}
