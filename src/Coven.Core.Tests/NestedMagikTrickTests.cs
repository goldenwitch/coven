// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class NestedMagikTrickTests
{
    private sealed class MarkOuter : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input, CancellationToken cancellationToken = default) { Tag.Add("outer:start"); return Task.FromResult(input); }
    }

    private sealed class InnerInc : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult(input + 1);
    }

    private sealed class ToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input);
    }

    private sealed class NonCandidateBig : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input, CancellationToken cancellationToken = default) => Task.FromResult((double)input + 9999d);
    }

    [Fact]
    public async Task Nested_Tricks_ChainThrough_InnerCandidates()
    {
        var coven = new MagikBuilder<int, double>()
            .MagikBlock<int, int>(new MarkOuter())
            // Outer Trick constrains next hop to its candidates. Strategy prefers Tricks after first hop.
            .MagikTrick<int, double, int>(
                configureCandidates: outer =>
                {
                    // Inner Trick is registered as an outer candidate
                    outer.MagikTrick<int, double, int>(
                        configureCandidates: inner =>
                        {
                            inner.MagikBlock<int, int>(new InnerInc());
                            inner.MagikBlock<int, double>(new ToDouble());
                        });
                })
            .Done();

        var result = await coven.Ritual<int, double>(10);
        // Outer Trick -> Inner Trick -> InnerInc (11) -> ToDouble => 11d
        Assert.Equal(11d, result);
    }

    [Fact]
    public async Task Nested_Tricks_Exclude_Outer_NonCandidates_OnFirstHop()
    {
        var coven = new MagikBuilder<int, double>()
            // Outer Trick constrains first hop to its candidates only
            .MagikTrick<int, double, int>(
                configureCandidates: outer =>
                {
                    // Outer candidate: Inner Trick
                    outer.MagikTrick<int, double, int>(
                        configureCandidates: inner =>
                        {
                            inner.MagikBlock<int, int>(new InnerInc());
                            inner.MagikBlock<int, double>(new ToDouble());
                        });
                })
            // Strong non-candidate that would otherwise win due to want:double
            .MagikBlock<int, double>(new NonCandidateBig(), new[] { "want:double" })
            .Done();

        var result = await coven.Ritual<int, double>(1);
        // First hop constrained to outer candidates: Inner Trick selected (not NonCandidateBig)
        // Then constrained to inner candidates: InnerInc (2)
        // After one hop, inner 'only:*' clears; ToDouble produces 2d rather than NonCandidateBig
        Assert.Equal(2d, result);
    }
}
