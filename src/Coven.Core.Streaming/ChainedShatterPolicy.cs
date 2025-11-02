// SPDX-License-Identifier: BUSL-1.1
namespace Coven.Core.Streaming;

/// <summary>
/// Applies shatter policies in sequence. Each policy runs over the current set
/// of entries; when a policy produces outputs for an entry, those outputs replace
/// the entry for the remainder of the chain. When a policy produces no outputs for
/// an entry, the entry is forwarded unchanged to subsequent policies.
///
/// Only entries produced by policies (i.e., transformed entries) are yielded.
/// Unchanged inputs are never yielded.
/// </summary>
public sealed class ChainedShatterPolicy<TEntry>(params IShatterPolicy<TEntry>[] policies) : IShatterPolicy<TEntry>
{
    private readonly IShatterPolicy<TEntry>[] _policies = policies ?? [];

    /// <summary>
    /// Applies each shatter policy in order to the provided entry, forwarding
    /// unchanged entries to subsequent policies and yielding only transformed outputs.
    /// </summary>
    /// <param name="entry">The source entry to shatter.</param>
    /// <returns>Zero or more transformed entries produced by the chain.</returns>
    public IEnumerable<TEntry> Shatter(TEntry entry)
    {
        if (_policies.Length == 0)
        {
            yield break; // no new entries
        }

        List<(TEntry Item, bool Changed)> current = [(entry, false)];

        foreach (IShatterPolicy<TEntry> policy in _policies)
        {
            List<(TEntry Item, bool Changed)> next = [];
            foreach ((TEntry Item, bool Changed) pair in current)
            {
                bool produced = false;
                IEnumerable<TEntry> outputs = policy.Shatter(pair.Item) ?? [];
                foreach (TEntry o in outputs)
                {
                    produced = true;
                    next.Add((o, true));
                }

                if (!produced)
                {
                    next.Add(pair); // forward unchanged for further policies
                }
            }
            current = next;
        }

        foreach ((TEntry Item, bool Changed) in current)
        {
            if (Changed)
            {
                yield return Item;
            }
        }
    }
}
