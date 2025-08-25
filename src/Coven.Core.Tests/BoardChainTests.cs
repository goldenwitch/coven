using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;

namespace Coven.Core.Tests;

public class BoardChainTests
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

    private static Board NewPrecompiledPushBoard(params MagikBlockDescriptor[] descriptors)
    {
        var registry = new List<MagikBlockDescriptor>(descriptors);
        var boardType = typeof(Board);
        var ctor = boardType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!, typeof(IReadOnlyList<MagikBlockDescriptor>), typeof(bool) },
            modifiers: null
        );
        var boardModeType = boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!;
        var pushEnum = Enum.Parse(boardModeType, "Push");
        return (Board)ctor!.Invoke(new object?[] { pushEnum, registry, true });
    }

    [Fact]
    public async Task PostWork_ComposesChain_DefaultsToNextRegistered()
    {
        // Competing first step: object->int vs string->int; should prefer the next registered (object->int) by default.
        var generic = new MagikBlockDescriptor(typeof(object), typeof(int), new ObjectToIntBlock(999));
        var specific = new MagikBlockDescriptor(typeof(string), typeof(int), new StringLengthBlock());
        // Second step to complete chain to target double
        var step2 = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDoubleBlock());

        var board = NewPushBoard(generic, specific, step2);

        var result = await board.PostWork<string, double>("abcd");
        // Expect generic first (999) -> 999d, proving order preference.
        Assert.Equal(999d, result);
    }

    [Fact]
    public async Task PostWork_DirectAssignable_ReturnsInput()
    {
        var board = NewPushBoard();
        var result = await board.PostWork<string, object>("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task PostWork_EmptyRegistry_NotAssignable_Throws()
    {
        var board = NewPushBoard();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await board.PostWork<string, int>("hello"));
    }

    [Fact]
    public async Task PostWork_NoChain_Throws()
    {
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new StringLengthBlock());
        var board = NewPushBoard(b1);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await board.PostWork<string, double>("abcd"));
    }

    [Fact]
    public async Task PostWork_UsesPrecompiledPipeline()
    {
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new StringLengthBlock());
        var b2 = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDoubleBlock());
        var board = NewPrecompiledPushBoard(b1, b2);
        var result = await board.PostWork<string, double>("abc");
        Assert.Equal(3d, result);
    }

    private sealed class StringLengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => Task.FromResult(input.Length);
    }

    private sealed class IntToDoubleBlock : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    private sealed class ObjectToIntBlock : IMagikBlock<object, int>
    {
        private readonly int value;
        public ObjectToIntBlock(int value) { this.value = value; }
        public Task<int> DoMagik(object input) => Task.FromResult(value);
    }

    [Fact]
    public async Task PostWork_Composes_AsyncBlocks_PropagatesAwait()
    {
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new AsyncStringLengthBlock());
        var b2 = new MagikBlockDescriptor(typeof(int), typeof(double), new AsyncIntToDoubleAddOne());
        var board = NewPushBoard(b1, b2);
        var result = await board.PostWork<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task PostWork_FinalSubtype_IsMappedToRequestedBase()
    {
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new StringLengthBlock());
        var b2 = new MagikBlockDescriptor(typeof(int), typeof(BaseAnimal), new IntToDogBlock());
        var board = NewPushBoard(b1, b2);
        BaseAnimal result = await board.PostWork<string, BaseAnimal>("abc");
        Assert.IsType<Dog>(result);
    }

    [Fact]
    public async Task PostWork_Awaits_AsyncDelays_BeforeCompletion()
    {
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new AsyncDelayThenLength(50));
        var b2 = new MagikBlockDescriptor(typeof(int), typeof(double), new AsyncDelayThenToDouble(50));
        var board = NewPushBoard(b1, b2);

        var sw = Stopwatch.StartNew();
        var result = await board.PostWork<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result); // length 4 then cast to double
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Pipeline finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PostWork_TieBreaksByRegistrationOrder_WhenSpecificityEqual()
    {
        var first = new MagikBlockDescriptor(typeof(string), typeof(int), new ReturnConstInt(1));
        var second = new MagikBlockDescriptor(typeof(string), typeof(int), new ReturnConstInt(2));
        var step2 = new MagikBlockDescriptor(typeof(int), typeof(double), new IntToDoubleBlock());
        var board = NewPushBoard(first, second, step2);
        var result = await board.PostWork<string, double>("ignored");
        Assert.Equal(1d, result);
    }

    [Fact]
    public async Task PostWork_PropagatesBlockException()
    {
        var failing = new MagikBlockDescriptor(typeof(string), typeof(int), new ThrowingBlock());
        var board = NewPushBoard(failing);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await board.PostWork<string, int>("x"));
    }

    // Async test blocks
    private sealed class AsyncStringLengthBlock : IMagikBlock<string, int>
    {
        public async Task<int> DoMagik(string input)
        {
            await Task.Delay(1).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncIntToDoubleAddOne : IMagikBlock<int, double>
    {
        public async Task<double> DoMagik(int input)
        {
            await Task.Delay(1).ConfigureAwait(false);
            return input + 1d;
        }
    }

    private sealed class AsyncDelayThenLength : IMagikBlock<string, int>
    {
        private readonly int delayMs;
        public AsyncDelayThenLength(int delayMs) { this.delayMs = delayMs; }
        public async Task<int> DoMagik(string input)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncDelayThenToDouble : IMagikBlock<int, double>
    {
        private readonly int delayMs;
        public AsyncDelayThenToDouble(int delayMs) { this.delayMs = delayMs; }
        public async Task<double> DoMagik(int input)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return (double)input;
        }
    }

    // Subtype mapping test types/blocks
    private class BaseAnimal { }
    private sealed class Dog : BaseAnimal { public int From { get; init; } }

    private sealed class IntToDogBlock : IMagikBlock<int, BaseAnimal>
    {
        public Task<BaseAnimal> DoMagik(int input) => Task.FromResult<BaseAnimal>(new Dog { From = input });
    }

    private sealed class ReturnConstInt : IMagikBlock<string, int>
    {
        private readonly int value;
        public ReturnConstInt(int value) { this.value = value; }
        public Task<int> DoMagik(string input) => Task.FromResult(value);
    }

    private sealed class ThrowingBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input) => throw new InvalidOperationException("boom");
    }
}
