// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using Coven.Core.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class BoardChainTests
{
    [Fact]
    public async Task PostWorkComposesChainDefaultsToNextRegistered()
    {
        // Competing first step: object->int vs string->int; should prefer the next registered (object->int) by default.
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.LambdaBlock<object, int>((_, ct) => Task.FromResult(999))
            .MagikBlock<string, int, StringLengthBlock>()
            .MagikBlock<int, double, IntToDoubleBlock>()
            .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abcd");
        // Expect generic first (999) -> 999d, proving order preference.
        Assert.Equal(999d, result);
    }

    [Fact]
    public async Task PostWorkDirectAssignableReturnsInput()
    {
        using TestHost host = TestBed.BuildPush(c => c.Done());
        // With empty registry, no precompiled pipelines
        Board board0 = Assert.IsType<Board>(host.Services.GetRequiredService<IBoard>());
        Assert.Equal(0, board0.Status.CompiledPipelinesCount);
        object result = await host.Coven.Ritual<string, object>("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task PostWorkEmptyRegistryNotAssignableThrows()
    {
        using TestHost host = TestBed.BuildPush(c => c.Done());
        // No entries -> no precompiled pipelines
        Board board = Assert.IsType<Board>(host.Services.GetRequiredService<IBoard>());
        Assert.Equal(0, board.Status.CompiledPipelinesCount);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.Coven.Ritual<string, int>("hello"));
    }

    [Fact]
    public async Task PostWorkNoChainThrows()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, StringLengthBlock>()
                .Done();
        });
        // With one block, some pipelines should be precompiled
        Board board1 = Assert.IsType<Board>(host.Services.GetRequiredService<IBoard>());
        Assert.True(board1.Status.CompiledPipelinesCount > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.Coven.Ritual<string, double>("abcd"));
    }

    private sealed class StringLengthBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(input.Length);
    }

    private sealed class IntToDoubleBlock : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    private sealed class ObjectToIntBlock(int value) : IMagikBlock<object, int>
    {
        public Task<int> DoMagik(object input, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    [Fact]
    public async Task PostWorkComposesAsyncBlocksPropagatesAwait()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, AsyncStringLengthBlock>()
                .MagikBlock<int, double, AsyncIntToDoubleAddOne>()
                .Done();
        });
        double result = await host.Coven.Ritual<string, double>("abcd");
        Assert.Equal(5d, result);
    }

    [Fact]
    public async Task PostWorkFinalSubtypeIsMappedToRequestedBase()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, StringLengthBlock>()
                .MagikBlock<int, BaseAnimal, IntToDogBlock>()
                .Done();
        });
        BaseAnimal result = await host.Coven.Ritual<string, BaseAnimal>("abc");
        Assert.IsType<Dog>(result);
    }

    [Fact]
    public async Task PostWorkAwaitsAsyncDelaysBeforeCompletion()
    {
        Stopwatch sw = Stopwatch.StartNew();
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.LambdaBlock<string, int>(async (s, ct) => { await Task.Delay(50, ct).ConfigureAwait(false); return s.Length; })
            .LambdaBlock<int, double>(async (i, ct) => { await Task.Delay(50, ct).ConfigureAwait(false); return i; })
            .Done();
        });

        double result = await host.Coven.Ritual<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result); // length 4 then cast to double
        Assert.True(sw.ElapsedMilliseconds >= 90, $"Pipeline finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PostWorkTieBreaksByRegistrationOrderWhenSpecificityEqual()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.LambdaBlock<string, int>((_, ct) => Task.FromResult(1))
            .LambdaBlock<string, int>((_, ct) => Task.FromResult(2))
            .MagikBlock<int, double, IntToDoubleBlock>()
            .Done();
        });
        double result = await host.Coven.Ritual<string, double>("ignored");
        Assert.Equal(1d, result);
    }

    [Fact]
    public async Task PostWorkPropagatesBlockException()
    {
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.MagikBlock<string, int, ThrowingBlock>()
                .Done();
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.Coven.Ritual<string, int>("x"));
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

    private sealed class AsyncDelayThenLength(int delayMs) : IMagikBlock<string, int>
    {
        public async Task<int> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncDelayThenToDouble(int delayMs) : IMagikBlock<int, double>
    {
        public async Task<double> DoMagik(int input, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            return input;
        }
    }

    // Subtype mapping test types/blocks
    private class BaseAnimal { }
    private sealed class Dog : BaseAnimal { public int From { get; init; } }

    private sealed class IntToDogBlock : IMagikBlock<int, BaseAnimal>
    {
        public Task<BaseAnimal> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult<BaseAnimal>(new Dog { From = input });
    }

    private sealed class ReturnConstInt(int value) : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class ThrowingBlock : IMagikBlock<string, int>
    {
        public Task<int> DoMagik(string input, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
    }
}
