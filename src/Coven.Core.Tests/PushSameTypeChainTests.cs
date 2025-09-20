// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PushSameTypeChainTests
{
    [Fact]
    public async Task Push_All_SameType_Blocks_RunInOrder()
    {
        int ran = 0;

        using var host = TestBed.BuildPush(c =>
        {
            c.AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|1"); });
            c.AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|2"); });
            c.AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|3"); });
            c.Done();
        });

        var result = await host.Coven.Ritual<string, string>("hi");

        Assert.Equal(3, ran);
        Assert.Equal("hi|1|2|3", result);
    }
}
