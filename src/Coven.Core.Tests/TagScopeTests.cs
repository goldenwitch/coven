// SPDX-License-Identifier: BUSL-1.1

using Xunit;
using Coven.Core.Tags;
using Coven.Core.Tests.Infrastructure;

namespace Coven.Core.Tests;

public class TagScopeTests
{

    [Fact]
    public void TagMethodsOutsideScopeThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Tag.Add("x"));
        Assert.Throws<InvalidOperationException>(() => Tag.Contains("x"));
        Assert.Throws<InvalidOperationException>(() => { var _ = Tag.Current; });
    }

    [Fact]
    public async Task TagMethodsAvailableWithinBoardScope()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, string, ProbeTagBlock>()
                .Done();
        });
        string result = await host.Coven.Ritual<string, string>("ignored");
        Assert.Equal("ok", result);
    }
}
