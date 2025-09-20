// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class PushNoShortCircuitMixedTypesTests
{
    [Fact]
    public async Task Push_MixedTypes_DoesNotShortCircuit_OnAssignable()
    {
        int finalRan = 0;

        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            // Step 1: string -> int
            c.AddLambda<string, int>((s, ct) => Task.FromResult(s.Length));
            // Step 2: int -> string (now assignable to TOut)
            c.AddLambda<int, string>((i, ct) => Task.FromResult($"len:{i}"));
            // Step 3: string -> string (should still run; no short-circuit)
            c.AddLambda<string, string>((s, ct) => { finalRan++; return Task.FromResult(s + "|final"); });
            c.Done(); // push mode
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, string>("abcd");

        Assert.Equal(1, finalRan); // ensure the last step executed
        Assert.Equal("len:4|final", result);
    }
}
