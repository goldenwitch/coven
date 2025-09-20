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
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<Counter, Counter, Inc>();                 // idx 0
            c.AddBlock<Counter, Counter, IncAndSignalCopy2>();   // idx 1
            c.AddBlock<Counter, Counter, Copy1>();               // idx 2
            c.AddBlock<Counter, Counter, Copy2>();               // idx 3
            c.AddBlock<Counter, double, ToDouble>();              // idx 4
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 0 });
        // 0 -> 1 (Inc) -> 2 (IncAndSignalCopy2) emits tag to Copy2 -> Copy1 would be next by order, but tag points to Copy2
        // -> 2 (Copy2) -> to double => 2d
        Assert.Equal(2d, result);
    }

    [Fact]
    public async Task Sequential_InitialTag_SkipsFirst_ToSpecificIndex()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<Counter, Counter, Inc>();                 // idx 0
            c.AddBlock<Counter, Counter, IncAndSignalCopy2>();   // idx 1
            c.AddBlock<Counter, Counter, Copy1>();               // idx 2
            c.AddBlock<Counter, Counter, Copy2>();               // idx 3
            c.AddBlock<Counter, double, ToDouble>();              // idx 4
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 0 }, new List<string> { "to:#1" });
        // Start at idx1 due to tag: 0 -> 1 (IncAndSignalCopy2, emits to:Copy2) -> Copy2 -> 1d
        Assert.Equal(1d, result);
    }
}
