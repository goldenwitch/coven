// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PushSameTypeChainTests
{
    [Fact]
    public async Task PushAllSameTypeBlocksRunInOrder()
    {
        int ran = 0;

        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|1"); })
                .AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|2"); })
                .AddLambda<string, string>((s, ct) => { ran++; return Task.FromResult(s + "|3"); })
                .Done();
        });

        string result = await host.Coven.Ritual<string, string>("hi");

        Assert.Equal(3, ran);
        Assert.Equal("hi|1|2|3", result);
    }
}
