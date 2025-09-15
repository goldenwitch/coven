// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class TagIncrementTests
{
    // Build pipelines via MagikBuilder for push mode.

    private sealed class Counter { public int Value { get; init; } }

    private sealed class Inc : IMagikBlock<Counter, Counter>
    {
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
            => Task.FromResult(new Counter { Value = input.Value + 1 });
    }

    private sealed class IncAndSignalCopy2 : IMagikBlock<Counter, Counter>
    {
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
        {
            Tag.Add("to:Copy2");
            return Task.FromResult(new Counter { Value = input.Value + 1 });
        }
    }

    private sealed class Copy1 : IMagikBlock<Counter, Counter>
    {
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
    }

    private sealed class Copy2 : IMagikBlock<Counter, Counter>
    {
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(new Counter { Value = input.Value });
    }

    private sealed class ToDouble : IMagikBlock<Counter, double>
    {
        public Task<double> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult((double)input.Value);
    }

    [Fact]
    public async Task Sequential_Default_NextByRegistrationOrder()
    {
        // Order: Inc -> IncAndSignalCopy2 -> Copy1 -> Copy2 -> ToDouble
        var coven = new MagikBuilder<Counter, double>()
            .MagikBlock(new Inc())                 // idx 0
            .MagikBlock<Counter, Counter>(new IncAndSignalCopy2())   // idx 1
            .MagikBlock<Counter, Counter>(new Copy1())               // idx 2
            .MagikBlock<Counter, Counter>(new Copy2())               // idx 3
            .MagikBlock<Counter, double>(new ToDouble())              // idx 4
            .Done();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 0 });
        // 0 -> 1 (Inc) -> 2 (IncAndSignalCopy2) emits tag to Copy2 -> Copy1 would be next by order, but tag points to Copy2
        // -> 2 (Copy2) -> to double => 2d
        Assert.Equal(2d, result);
    }

    [Fact]
    public async Task Sequential_InitialTag_SkipsFirst_ToSpecificIndex()
    {
        var coven = new MagikBuilder<Counter, double>()
            .MagikBlock(new Inc())                 // idx 0
            .MagikBlock<Counter, Counter>(new IncAndSignalCopy2())   // idx 1
            .MagikBlock<Counter, Counter>(new Copy1())               // idx 2
            .MagikBlock<Counter, Counter>(new Copy2())               // idx 3
            .MagikBlock<Counter, double>(new ToDouble())              // idx 4
            .Done();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 0 }, new List<string> { "to:#1" });
        // Start at idx1 due to tag: 0 -> 1 (IncAndSignalCopy2, emits to:Copy2) -> Copy2 -> 1d
        Assert.Equal(1d, result);
    }
}
