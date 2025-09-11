// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class TagRoutingTests
{
    // Routing precedence per step: to:* → capability overlap → registration order.

    private sealed class ReturnConstInt : IMagikBlock<string, int>
    {
        private readonly int value;
        public ReturnConstInt(int value) { this.value = value; }
        public Task<int> DoMagik(string input) => Task.FromResult(value);
    }

    private sealed class IntToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    private sealed class IntToDoubleAddOne : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input + 1d);
    }

    [Fact]
    public async Task Routing_Honors_ToIndex_Tag_OnFirstStep()
    {
        // Two competing string->int blocks; select the second by to:#1
        var coven = new MagikBuilder<string, double>()
            .MagikBlock(new ReturnConstInt(1)) // idx 0
            .MagikBlock<string, int>(new ReturnConstInt(2)) // idx 1
            .MagikBlock<int, double>(new IntToDouble())
            .Done();

        var result = await coven.Ritual<string, double>("x", new List<string> { "to:#1" });
        Assert.Equal(2d, result);
    }

    private sealed class EmitNextPreference : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input)
        {
            // Prefer the AddOne variant by type name
            Tag.Add("to:IntToDoubleAddOne");
            return Task.FromResult(input.Length);
        }
    }

    [Fact]
    public async Task Routing_Uses_Block_Emitted_ToType_Tag_ForNextStep()
    {
        var coven = new MagikBuilder<string, double>()
            .MagikBlock(new EmitNextPreference())
            .MagikBlock<int, double>(new IntToDouble())
            .MagikBlock<int, double>(new IntToDoubleAddOne())
            .Done();

        var result = await coven.Ritual<string, double>("abc");
        // length=3, routed to AddOne -> 4
        Assert.Equal(4d, result);
    }
}