// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Coven.Core.Di;
using Coven.Core.Tests.Infrastructure;

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

    [Fact]
    public async Task Tag_Methods_Available_WithinBoardScope()
    {
        using var host = TestBed.BuildPush(c =>
        {
            c.AddBlock<string, string, ProbeTag>();
            c.Done();
        });
        var result = await host.Coven.Ritual<string, string>("ignored");
        Assert.Equal("ok", result);
    }
}
