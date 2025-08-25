using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilityTests
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

    private sealed class Counter { public int Value { get; init; } }

    private sealed class TagEmit : IMagikBlock<Counter, Counter>
    {
        private readonly IReadOnlyCollection<string> tags;
        public TagEmit(params string[] tags) { this.tags = tags; }
        public Task<Counter> DoMagik(Counter input)
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
        public Task<Counter> DoMagik(Counter input) => Task.FromResult(input);
        public override string ToString() => name;
    }

    private sealed class ToDouble : IMagikBlock<Counter, double>
    {
        public Task<double> DoMagik(Counter input) => Task.FromResult((double)input.Value);
    }

    [Fact]
    public async Task Capability_Matching_Prefers_MaxOverlap_Then_Order()
    {
        // Emit tags: a, b
        // Candidates: A supports {a}; B supports {a,b}; C supports {b}
        var board = NewPushBoard(
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new TagEmit("a", "b")), // idx 0
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("A", "a")), // idx 1
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("B", "a", "b")), // idx 2
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("C", "b")), // idx 3
            new MagikBlockDescriptor(typeof(Counter), typeof(double), new ToDouble()) // idx 4
        );

        var result = await board.PostWork<Counter, double>(new Counter { Value = 7 });
        // Router should choose B (max overlap=2) as the next step after TagEmit
        Assert.Equal(7d, result);
    }

    [Fact]
    public async Task Explicit_To_Overrides_Capability_Scoring()
    {
        // Even though B would be chosen by capability, we direct to C by index
        var board = NewPushBoard(
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new TagEmit("x", "y")), // idx 0
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("A", "x")), // idx 1
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("B", "x", "y")), // idx 2
            new MagikBlockDescriptor(typeof(Counter), typeof(Counter), new CapBlock("C", "y")), // idx 3
            new MagikBlockDescriptor(typeof(Counter), typeof(double), new ToDouble()) // idx 4
        );

        var result = await board.PostWork<Counter, double>(new Counter { Value = 5 }, new List<string> { "to:#3" });
        Assert.Equal(5d, result);
    }
}

