// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PushNoShortCircuitMixedTypesTests
{
    [Fact]
    public async Task PushMixedTypesDoesNotShortCircuitOnAssignable()
    {
        int finalRan = 0;

        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.LambdaBlock<string, int>((s, ct) => Task.FromResult(s.Length))
                .LambdaBlock<int, string>((i, ct) => Task.FromResult($"len:{i}"))
                .LambdaBlock<string, string>((s, ct) => { finalRan++; return Task.FromResult(s + "|final"); })
                .Done();
        });

        string result = await host.Coven.Ritual<string, string>("abcd");

        Assert.Equal(1, finalRan); // ensure the last step executed
        Assert.Equal("len:4|final", result);
    }
}
