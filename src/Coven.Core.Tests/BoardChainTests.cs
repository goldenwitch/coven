using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class BoardChainTests
{

    

    [Fact]
    public async Task PostWork_ComposesChain_DefaultsToNextRegistered()
    {
        // Competing first step: object->int vs string->int; should prefer the next registered (object->int) by default.
        var coven = new MagikBuilder<string, double>()
            .MagikBlock<object, int>(new ObjectToIntBlock(999))
            .MagikBlock<string, int>(new StringLengthBlock())
            .MagikBlock<int, double>(new IntToDoubleBlock())
            .Done();

        var result = await coven.Ritual<string, double>("abcd");
        // Expect generic first (999) -> 999d, proving order preference.
        Assert.Equal(999d, result);
    }

    [Fact]
    public async Task PostWork_DirectAssignable_ReturnsInput()
    {
        var coven = new MagikBuilder<string, object>()
            .Done();
        var result = await coven.Ritual<string, object>("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task PostWork_EmptyRegistry_NotAssignable_Throws()
    {
        var coven = new MagikBuilder<string, int>()
            .Done();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await coven.Ritual<string, int>("hello"));
    }

    [Fact]
    public async Task PostWork_NoChain_Throws()
    {
        var coven = new MagikBuilder<string, double>()
            .MagikBlock<string, int>(new StringLengthBlock())
            .Done();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await coven.Ritual<string, double>("abcd"));
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
        var coven = new MagikBuilder<string, double>()
            .MagikBlock<string, int>(new AsyncStringLengthBlock())
            .MagikBlock<int, double>(new AsyncIntToDoubleAddOne())
            .Done();
        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task PostWork_FinalSubtype_IsMappedToRequestedBase()
    {
        var coven = new MagikBuilder<string, BaseAnimal>()
            .MagikBlock<string, int>(new StringLengthBlock())
            .MagikBlock<int, BaseAnimal>(new IntToDogBlock())
            .Done();
        BaseAnimal result = await coven.Ritual<string, BaseAnimal>("abc");
        Assert.IsType<Dog>(result);
    }

    [Fact]
    public async Task PostWork_Awaits_AsyncDelays_BeforeCompletion()
    {
        var sw = Stopwatch.StartNew();
        var covenDelay = new MagikBuilder<string, double>()
            .MagikBlock<string, int>(new AsyncDelayThenLength(50))
            .MagikBlock<int, double>(new AsyncDelayThenToDouble(50))
            .Done();

        var result = await covenDelay.Ritual<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result); // length 4 then cast to double
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Pipeline finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PostWork_TieBreaksByRegistrationOrder_WhenSpecificityEqual()
    {
        var coven = new MagikBuilder<string, double>()
            .MagikBlock<string, int>(new ReturnConstInt(1))
            .MagikBlock<string, int>(new ReturnConstInt(2))
            .MagikBlock<int, double>(new IntToDoubleBlock())
            .Done();
        var result = await coven.Ritual<string, double>("ignored");
        Assert.Equal(1d, result);
    }

    [Fact]
    public async Task PostWork_PropagatesBlockException()
    {
        var covenFail = new MagikBuilder<string, int>()
            .MagikBlock<string, int>(new ThrowingBlock())
            .Done();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await covenFail.Ritual<string, int>("x"));
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
