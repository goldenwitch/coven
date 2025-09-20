// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PushNoShortCircuitMixedTypesTests
{
    [Fact]
    public async Task Push_MixedTypes_DoesNotShortCircuit_OnAssignable()
    {
        int finalRan = 0;

        using var host = TestBed.BuildPush(c =>
        {
            c.AddLambda<string, int>((s, ct) => Task.FromResult(s.Length));
            c.AddLambda<int, string>((i, ct) => Task.FromResult($"len:{i}"));
            c.AddLambda<string, string>((s, ct) => { finalRan++; return Task.FromResult(s + "|final"); });
            c.Done();
        });

        var result = await host.Coven.Ritual<string, string>("abcd");

        Assert.Equal(1, finalRan); // ensure the last step executed
        Assert.Equal("len:4|final", result);
    }
}
