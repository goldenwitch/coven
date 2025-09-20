// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class TagRoutingTests
{
    // Routing precedence per step: to:* → capability overlap → registration order.

    private sealed class ReturnConstInt : IMagikBlock<string, int>
    {
        private readonly int value;
        public ReturnConstInt(int value) { this.value = value; }
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    private sealed class IntToDoubleAddOne : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input + 1d);
    }

    [Fact]
    public async Task Routing_Honors_ToIndex_Tag_OnFirstStep()
    {
        // Two competing string->int blocks; select the second by to:#1
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int>(sp => new ReturnConstInt(1));
            c.AddBlock<string, int>(sp => new ReturnConstInt(2));
            c.AddBlock<int, double, IntToDouble>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("x", new List<string> { "to:#1" });
        Assert.Equal(2d, result);
    }

    private sealed class EmitNextPreference : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            // Prefer the AddOne variant by type name
            Tag.Add("to:IntToDoubleAddOne");
            return Task.FromResult(input.Length);
        }
    }

    [Fact]
    public async Task Routing_Uses_Block_Emitted_ToType_Tag_ForNextStep()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, EmitNextPreference>();
            c.AddBlock<int, double, IntToDouble>();
            c.AddBlock<int, double, IntToDoubleAddOne>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abc");
        // length=3, routed to AddOne -> 4
        Assert.Equal(4d, result);
    }
}
