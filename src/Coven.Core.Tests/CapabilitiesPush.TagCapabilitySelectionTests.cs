// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Tests.Infrastructure;
using Xunit;

namespace Coven.Core.Tests;

public class TagCapabilitySelectionTests
{

    private sealed class Counter { public int Value { get; init; } }

    private sealed class TagEmit(params string[] tags) : IMagikBlock<Counter, Counter>
    {
        private readonly IReadOnlyCollection<string> _tags = tags;


        public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default)
        {
            foreach (string t in _tags)
            {
                Tag.Add(t);
            }


            return Task.FromResult(input);
        }
    }

    private sealed class CapBlock(string name, params string[] caps) : IMagikBlock<Counter, Counter>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags { get; } = caps; public Task<Counter> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult(input);
        public override string ToString() => name;
    }

    private sealed class ToDouble : IMagikBlock<Counter, double>
    {
        public Task<double> DoMagik(Counter input, CancellationToken cancellationToken = default) => Task.FromResult((double)input.Value);
    }

    [Fact]
    public async Task CapabilityMatchingPrefersMaxOverlapThenOrder()
    {
        // Emit tags: a, b; expect B due to overlap=2.
        // Candidates: A supports {a}; B supports {a,b}; C supports {b}
        using TestHost host = TestBed.BuildPush(c =>
        {
            _ = c.LambdaBlock<Counter, Counter>((input, ct) => { Tag.Add("a"); Tag.Add("b"); return Task.FromResult(input); })
                .LambdaBlock<Counter, Counter>((x, ct) => Task.FromResult(x), capabilities: ["a"])
                .LambdaBlock<Counter, Counter>((x, ct) => Task.FromResult(x), capabilities: ["a", "b"])
                .LambdaBlock<Counter, Counter>((x, ct) => Task.FromResult(x), capabilities: ["b"])
                .MagikBlock<Counter, double, ToDouble>()
                .Done();
        });

        double result = await host.Coven.Ritual<Counter, double>(new Counter { Value = 7 });
        // Router should choose B (max overlap=2) as the next step after TagEmit
        Assert.Equal(7d, result);
    }

}
