// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class PushSameTypeChainTests
{
    [Fact]
    public async Task Push_All_SameType_Blocks_RunInOrder()
    {
        int ran = 0;

        var coven = new MagikBuilder<string, string>()
            .MagikBlock<string, string>(s => { ran++; return Task.FromResult(s + "|1"); })
            .MagikBlock<string, string>(s => { ran++; return Task.FromResult(s + "|2"); })
            .MagikBlock<string, string>(s => { ran++; return Task.FromResult(s + "|3"); })
            .Done(); // push mode

        var result = await coven.Ritual<string, string>("hi");

        Assert.Equal(3, ran);
        Assert.Equal("hi|1|2|3", result);
    }
}
