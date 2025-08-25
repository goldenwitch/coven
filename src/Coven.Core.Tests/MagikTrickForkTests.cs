using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;
using Xunit.Sdk;

namespace Coven.Core.Tests;

public class MagikTrickForkTests
{
    private sealed class EmitA : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input)
        {
            Tag.Add("from:A");
            return Task.FromResult(input);
        }
    }

    private sealed class EmitB : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input)
        {
            Tag.Add("from:B");
            return Task.FromResult(input);
        }
    }

    private sealed class ToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    private sealed class AddThousand : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input) => Task.FromResult(input + 1000);
    }

    private sealed class CandidateToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input);
    }

    private sealed class NonCandidateToDoubleBig : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input + 10000d);
    }

    private sealed class EmitExtra : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input) { Tag.Add("extra:match"); return Task.FromResult(input); }
    }

    private sealed class CandidateInc : IMagikBlock<int, int>
    {
        public Task<int> DoMagik(int input) => Task.FromResult(input + 1);
    }

    private sealed class NonCandidatePreferredToDouble : IMagikBlock<int, double>
    {
        public Task<double> DoMagik(int input) => Task.FromResult((double)input + 1000d);
    }

    [Fact]
    public async Task Trick_RoutesTo_ToDouble_WhenFromA()
    {
        var coven = new MagikBuilder<int, double>()
            .MagikBlock<int, int>(new EmitA())
            .MagikTrick<int, double, int>(
                chooseTags: (tags, x) => tags.Contains("from:A") ? new[] { "want:double" } : new[] { "want:thousand" },
                configureCandidates: b =>
                {
                    b.MagikBlock<int, double>(new ToDouble(), new[] { "want:double" });
                    b.MagikBlock<int, int>(new AddThousand(), new[] { "want:thousand" });
                    b.MagikBlock<int, double>(new ToDouble(), new[] { "want:double" }); // ensure double reachable after AddThousand
                })
            .Done();

        var result = await coven.Ritual<int, double>(42);
        Assert.Equal(42d, result);
    }

    [Fact]
    public async Task Trick_RoutesTo_AddThousand_WhenFromB_Then_ToDouble()
    {
        var coven = new MagikBuilder<int, double>()
            .MagikBlock<int, int>(new EmitB())
            .MagikTrick<int, double, int>(
                chooseTags: (tags, x) => tags.Contains("from:A") ? new[] { "want:double" } : new[] { "want:thousand" },
                configureCandidates: b =>
                {
                    b.MagikBlock<int, double>(new ToDouble(), new[] { "want:double" });
                    b.MagikBlock<int, int>(new AddThousand(), new[] { "want:thousand" });
                    b.MagikBlock<int, double>(new ToDouble(), new[] { "want:double" });
                })
            .Done();

        var result = await coven.Ritual<int, double>(2);
        // EmitB -> Trick chooses AddThousand -> value 1002 -> ToDouble => 1002d
        Assert.Equal(1002d, result);
    }

    [Fact]
    public async Task Trick_Excludes_NonCandidates_EvenWithBetterCapabilities()
    {
        // Emit a tag that the non-candidate also matches, making it a better match if not constrained.
        var coven = new MagikBuilder<int, double>()
            .MagikBlock<int, int>(new EmitExtra())
            .MagikTrick<int, double, int>(
                chooseTags: (tags, x) => new[] { "want:double" },
                configureCandidates: b =>
                {
                    // Candidate marked only with want:double
                    b.MagikBlock<int, double>(new CandidateToDouble(), new[] { "want:double" });
                })
            // Non-candidate has both want:double and extra:match (better capability overlap),
            // but should be excluded by the Trick's only:* constraint.
            .MagikBlock<int, double>(new NonCandidateToDoubleBig(), new[] { "want:double", "extra:match" })
            .Done();


        var result = await coven.Ritual<int, double>(1);
        Assert.Equal(1d, result); // would be 10001d if non-candidate won
    }

    [Fact]
    public async Task Trick_OnlyToken_IsSingleHop_Then_NormalRoutingResumes()
    {
        var coven = new MagikBuilder<int, double>()
            .MagikTrick<int, double, int>(
                // Direct immediate next hop to candidate increment; also seed preference for non-candidate later.
                chooseTags: (tags, x) => new[] { "prefer:inc", "prefer:noncand" },
                configureCandidates: b =>
                {
                    b.MagikBlock<int, int>(new CandidateInc(), new[] { "prefer:inc" }); // consumes only:* constraint
                    b.MagikBlock<int, double>(new CandidateToDouble()); // candidate available but not preferred later
                })
            // After one hop, only:* is cleared; non-candidate with better capability should win next.
            .MagikBlock<int, double>(new NonCandidatePreferredToDouble(), new[] { "prefer:noncand" })
            .Done();

        var result = await coven.Ritual<int, double>(10);
        // Trick -> CandidateInc (11) [only:* consumed] -> NonCandidatePreferredToDouble (+1000) => 1011d
        Assert.Equal(1011d, result);
    }
}
