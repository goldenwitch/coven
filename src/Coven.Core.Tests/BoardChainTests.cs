// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class BoardChainTests
{

    

    [Fact]
    public async Task PostWork_ComposesChain_DefaultsToNextRegistered()
    {
        // Competing first step: object->int vs string->int; should prefer the next registered (object->int) by default.
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<object, int>(sp => new ObjectToIntBlock(999));
            c.AddBlock<string, int, StringLengthBlock>();
            c.AddBlock<int, double, IntToDoubleBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<string, double>("abcd");
        // Expect generic first (999) -> 999d, proving order preference.
        Assert.Equal(999d, result);
    }

    [Fact]
    public async Task PostWork_DirectAssignable_ReturnsInput()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c => c.Done());
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, object>("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task PostWork_EmptyRegistry_NotAssignable_Throws()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c => c.Done());
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await coven.Ritual<string, int>("hello"));
    }

    [Fact]
    public async Task PostWork_NoChain_Throws()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringLengthBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await coven.Ritual<string, double>("abcd"));
    }

    

    private sealed class StringLengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    private sealed class IntToDoubleBlock : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    private sealed class ObjectToIntBlock : IMagikBlock<object, int>
    {
        private readonly int value;
        public ObjectToIntBlock(int value) { this.value = value; }
        public Task<int> DoMagik(object input, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    [Fact]
    public async Task PostWork_Composes_AsyncBlocks_PropagatesAwait()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, AsyncStringLengthBlock>();
            c.AddBlock<int, double, AsyncIntToDoubleAddOne>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task PostWork_FinalSubtype_IsMappedToRequestedBase()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, StringLengthBlock>();
            c.AddBlock<int, BaseAnimal, IntToDogBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        BaseAnimal result = await coven.Ritual<string, BaseAnimal>("abc");
        Assert.IsType<Dog>(result);
    }

    [Fact]
    public async Task PostWork_Awaits_AsyncDelays_BeforeCompletion()
    {
        var sw = Stopwatch.StartNew();
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int>(sp => new AsyncDelayThenLength(50));
            c.AddBlock<int, double>(sp => new AsyncDelayThenToDouble(50));
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var covenDelay = sp.GetRequiredService<ICoven>();

        var result = await covenDelay.Ritual<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result); // length 4 then cast to double
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Pipeline finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PostWork_TieBreaksByRegistrationOrder_WhenSpecificityEqual()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int>(sp => new ReturnConstInt(1));
            c.AddBlock<string, int>(sp => new ReturnConstInt(2));
            c.AddBlock<int, double, IntToDoubleBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();
        var result = await coven.Ritual<string, double>("ignored");
        Assert.Equal(1d, result);
    }

    [Fact]
    public async Task PostWork_PropagatesBlockException()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, int, ThrowingBlock>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var covenFail = sp.GetRequiredService<ICoven>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await covenFail.Ritual<string, int>("x"));
    }

    // Async test blocks
    private sealed class AsyncStringLengthBlock : IMagikBlock<string, int>
    {
        public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncIntToDoubleAddOne : IMagikBlock<int, double>
    {
        public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return input + 1d;
        }
    }

    private sealed class AsyncDelayThenLength : IMagikBlock<string, int>
    {
        private readonly int delayMs;
        public AsyncDelayThenLength(int delayMs) { this.delayMs = delayMs; }
        public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncDelayThenToDouble : IMagikBlock<int, double>
    {
        private readonly int delayMs;
        public AsyncDelayThenToDouble(int delayMs) { this.delayMs = delayMs; }
        public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return (double)input;
        }
    }

    // Subtype mapping test types/blocks
    private class BaseAnimal { }
    private sealed class Dog : BaseAnimal { public int From { get; init; } }

    private sealed class IntToDogBlock : IMagikBlock<int, BaseAnimal>
    {
        public Task<BaseAnimal> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult<BaseAnimal>(new Dog { From = input });
    }

    private sealed class ReturnConstInt : IMagikBlock<string, int>
    {
        private readonly int value;
        public ReturnConstInt(int value) { this.value = value; }
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class ThrowingBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
    }
}
