using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class TagRoutingTests
{
    private static Board NewPushBoard(params MagikBlockDescriptor[] descriptors)
    {
        var registry = new List<MagikBlockDescriptor>(descriptors);
        var boardType = typeof(Board);
        var ctor = boardType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!, typeof(IReadOnlyList<MagikBlockDescriptor>) },
            modifiers: null
        );
        var boardModeType = boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!;
        var pushEnum = Enum.Parse(boardModeType, "Push");
        return (Board)ctor!.Invoke(new object?[] { pushEnum, registry });
    }

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
        var b0 = new MagikBlockDescriptor(typeof(string), typeof(int), new ReturnConstInt(1)); // index 0
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new ReturnConstInt(2)); // index 1
        var step2 = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDouble());
        var board = NewPushBoard(b0, b1, step2);

        var result = await board.PostWork<string, double>("x", new List<string> { "to:#1" });
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
        var first = new MagikBlockDescriptor(typeof(string), typeof(int), new EmitNextPreference());
        var normal = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDouble());
        var addOne = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDoubleAddOne());
        var board = NewPushBoard(first, normal, addOne);

        var result = await board.PostWork<string, double>("abc");
        // length=3, routed to AddOne -> 4
        Assert.Equal(4d, result);
    }
}

