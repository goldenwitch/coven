// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class TypeDrivenFinalityTests
{
    // Assert that declared generic TOut controls finality in pull mode,
    // not the runtime type of the value produced by earlier steps.
    [Fact]
    public async Task PullDeclaredTypeDrivesFinalityNotRuntime()
    {
        using TestHost host = TestBed.BuildPull(c =>
        {
            c.MagikBlock<Start, object, ToObjectBlock>()
             .MagikBlock<object, string, ObjectToStringPlus>();
        });

        string result = await host.Coven.Ritual<Start, string>(new Start { Value = "x" });
        Assert.Equal("x|b2", result);
    }
}
