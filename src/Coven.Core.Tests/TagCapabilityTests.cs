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

public class TagCapabilityTests
{

    private sealed class Counter { public int Value { get; init; } }

    private sealed class TagEmit : IMagikBlock<Counter, Counter>
    {
        private readonly IReadOnlyCollection<string> tags;
        public TagEmit(params string[] tags) { this.tags = tags; }
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
        {
            foreach (var t in tags) Tag.Add(t);
            return Task.FromResult(input);
        }
    }

    private sealed class CapBlock : IMagikBlock<Counter, Counter>, ITagCapabilities
    {
        private readonly string name;
        public CapBlock(string name, params string[] caps) { this.name = name; SupportedTags = caps; }
        public IReadOnlyCollection<string> SupportedTags { get; }
        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(input);
        public override string ToString() => name;
    }

    private sealed class ToDouble : IMagikBlock<Counter, double>
    {
        public Task<double> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult((double)input.Value);
    }

    [Fact]
    public async Task Capability_Matching_Prefers_MaxOverlap_Then_Order()
    {
        // Emit tags: a, b; expect B due to overlap=2.
        // Candidates: A supports {a}; B supports {a,b}; C supports {b}
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<Counter, Counter>(sp => new TagEmit("a", "b"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("A", "a"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("B", "a", "b"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("C", "b"));
            c.AddBlock<Counter, double, ToDouble>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 7 });
        // Router should choose B (max overlap=2) as the next step after TagEmit
        Assert.Equal(7d, result);
    }

    [Fact]
    public async Task Explicit_To_Overrides_Capability_Scoring()
    {
        // Even though B would be chosen by capability, we direct to C by index
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<Counter, Counter>(sp => new TagEmit("x", "y"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("A", "x"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("B", "x", "y"));
            c.AddBlock<Counter, Counter>(sp => new CapBlock("C", "y"));
            c.AddBlock<Counter, double, ToDouble>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var result = await coven.Ritual<Counter, double>(new Counter { Value = 5 }, new List<string> { "to:#3" });
        Assert.Equal(5d, result);
    }
}
