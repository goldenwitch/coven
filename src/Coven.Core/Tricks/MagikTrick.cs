using System;
using System.Collections.Generic;
using System.Linq;
using Coven.Core.Tags;

namespace Coven.Core.Tricks;

// Trick-as-fork: a special MagikBlock that returns input unchanged (T->T)
// and emits tags to steer the next block selection. It can also constrain
// routing to a predefined candidate set via an internal limit token.
public sealed class MagikTrick<T> : IMagikBlock<T, T>, ITagCapabilities, IMagikTrick
{
    private readonly Func<ISet<string>, T, IEnumerable<string>?> chooseTags;
    private readonly IReadOnlyCollection<string> caps;
    private List<CandidateRef>? candidateRefs; // candidates and their builder-assigned capabilities

    internal sealed class CandidateRef
    {
        public required object Instance { get; init; }
        public required List<string> Capabilities { get; init; }
        public string TypeName => Instance.GetType().Name;
    }

    public MagikTrick(Func<ISet<string>, T, IEnumerable<string>?> chooseTags, IEnumerable<string>? capabilities = null)
    {
        this.chooseTags = chooseTags ?? throw new ArgumentNullException(nameof(chooseTags));
        caps = capabilities is null ? Array.Empty<string>() : capabilities.ToArray();
    }

    internal void SetCandidates(IEnumerable<CandidateRef> refs)
        => candidateRefs = refs.ToList();

    public Task<T> DoMagik(T input)
    {
        if (candidateRefs is not null)
        {
            // Hard fence next hop to trick candidates only
            Tag.SetNextSelectionFence(candidateRefs.Select(r => r.Instance));
        }

        var tags = chooseTags(Tag.Current, input)?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();
        foreach (var t in tags) Tag.Add(t);

        // Optional: compute best candidate and set explicit to:<TypeName> to steer within fence
        if (candidateRefs is not null && candidateRefs.Count > 0)
        {
            int bestScore = int.MinValue;
            int bestIdx = 0;
            string? bestTypeName = null;
            for (int i = 0; i < candidateRefs.Count; i++)
            {
                var r = candidateRefs[i];
                // Merge builder-assigned and runtime-advertised capabilities
                var merged = new HashSet<string>(r.Capabilities, StringComparer.OrdinalIgnoreCase);
                if (r.Instance is ITagCapabilities tc && tc.SupportedTags is not null)
                {
                    foreach (var c in tc.SupportedTags) merged.Add(c);
                }
                int score = 0;
                foreach (var t in tags)
                {
                    if (merged.Contains(t)) score++;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                    bestTypeName = r.TypeName;
                }
            }
            if (!string.IsNullOrEmpty(bestTypeName)) Tag.Add($"to:{bestTypeName}");
        }
        return Task.FromResult(input);
    }

    public IReadOnlyCollection<string> SupportedTags => caps;
}
