// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class PullBehaviorTests
{
    // Assert that declared generic TOut controls finality in pull mode,
    // not the runtime type of the value produced by earlier steps.
    [Fact]
    public async Task Pull_DeclaredType_Drives_Finality_Not_Runtime()
    {
        using var host = TestBed.BuildPull(c =>
        {
            c.AddBlock<Start, object, ToObject>();
            c.AddBlock<object, string, ObjectToStringPlus>();
        });

        var result = await host.Coven.Ritual<Start, string>(new Start { Value = "x" });
        Assert.Equal("x|b2", result);
    }
}
