// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;

namespace Coven.Core.Tricks;

// Trick-as-fork: a special MagikBlock that returns input unchanged (T->T)
// and constrains routing to a predefined candidate set via an internal fence.
// Selection among the fenced candidates is left to the configured ISelectionStrategy.
public sealed class MagikTrick<T> : IMagikBlock<T, T>, ITagCapabilities, IMagikTrick
{
    private readonly IReadOnlyCollection<string> caps;
    private List<CandidateRef>? candidateRefs; // candidates and their builder-assigned capabilities

    internal sealed class CandidateRef
    {
        public required object Instance { get; init; }
        public required List<string> Capabilities { get; init; }
        public string TypeName => Instance.GetType().Name;
    }

    public MagikTrick(IEnumerable<string>? capabilities = null)
        => caps = capabilities is null ? Array.Empty<string>() : capabilities.ToArray();

    internal void SetCandidates(IEnumerable<CandidateRef> refs)
        => candidateRefs = refs.ToList();

    public Task<T> DoMagik(T input)
    {
        if (candidateRefs is not null)
        {
            // Hard fence next hop to trick candidates only
            Tag.SetNextSelectionFence(candidateRefs.Select(r => r.Instance));
        }
        return Task.FromResult(input);
    }

    public IReadOnlyCollection<string> SupportedTags => caps;
}