// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Coven.Core.Builder;
using Coven.Core.Tags;

namespace Coven.Core.Tests;

public class TagScopeTests
{

    [Fact]
    public void Tag_Methods_OutsideScope_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => Tag.Add("x"));
        Assert.Throws<InvalidOperationException>(() => Tag.Contains("x"));
        Assert.Throws<InvalidOperationException>(() => { var _ = Tag.Current; });
    }

    private sealed class ProbeBlock : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            Tag.Add("probe");
            var ok = Tag.Contains("probe");
            return Task.FromResult(ok ? "ok" : "bad");
        }
    }

    [Fact]
    public async Task Tag_Methods_Available_WithinBoardScope()
    {
        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new ProbeBlock())
            .Done();
        var result = await coven.Ritual<string, string>("ignored");
        Assert.Equal("ok", result);
    }
}
