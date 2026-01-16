// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents;

/// <summary>
/// Shatters AgentThought outputs on the first matched "summary marker":
/// any bold Markdown segment ("**...**") immediately followed by a newline sequence
/// ("\n\n", "\r\n\r\n", or "\r\n").
///
/// When a boundary is found, emits two AgentThought entries:
/// - First: everything before the bold segment
/// - Second: the bold segment plus the newline sequence and any remaining text
///
/// If no boundary exists, produces no outputs (forward unchanged).
/// </summary>
public sealed class AgentThoughtSummaryShatterPolicy : IShatterPolicy<AgentEntry>
{
    private static class Grammar
    {
        // Token for a Markdown bold delimiter
        public const string Bold = "**";
        // Ordered newline sequences that define a "paragraph boundary"
        public static readonly string[] _paragraphBoundaries = ["\r\n\r\n", "\n\n", "\r\n"];
    }

    /// <summary>
    /// Splits an <see cref="AgentThought"/> into up to two entries at the first detected summary marker boundary.
    /// </summary>
    /// <param name="entry">The source agent entry.</param>
    /// <returns>
    /// Zero or more <see cref="AgentEntry"/> instances: at most two <see cref="AgentThought"/> parts
    /// when a boundary is present; otherwise no output.
    /// </returns>
    public IEnumerable<AgentEntry> Shatter(AgentEntry entry)
    {
        if (entry is not AgentThought thought || string.IsNullOrEmpty(thought.Text))
        {
            yield break;
        }

        string text = thought.Text;
        // Locate the first boundary where a bold segment is immediately followed by a newline sequence.
        // The returned index is the position of the bold opener; we split BEFORE it.
        int splitIndex = IndexOfSummaryBoundary(text);
        if (splitIndex < 0)
        {
            yield break;
        }

        // Split before the header: first = preface text; second = header + newline(s) + remainder.
        string first = text[..splitIndex];
        string second = text[splitIndex..];

        // Emit only non-empty chunks.
        if (first.Length > 0)
        {
            yield return new AgentThought(thought.Sender, first);
        }
        if (second.Length > 0)
        {
            yield return new AgentThought(thought.Sender, second);
        }
    }

    private static int IndexOfSummaryBoundary(string s)
    {
        ReadOnlySpan<char> span = s.AsSpan();
        int position = 0;
        while (position < span.Length)
        {
            // Find the next bold opener starting at the current scan position.
            int start = span[position..].IndexOf(Grammar.Bold);
            if (start < 0)
            {
                return -1;
            }
            // Convert relative index to absolute index within the source span.
            start += position;

            // Index immediately after the opening bold token.
            int afterOpen = start + Grammar.Bold.Length;
            if (afterOpen >= span.Length) { return -1; }

            // Search for the matching bold closer after the opener.
            int end = span[afterOpen..].IndexOf(Grammar.Bold);
            if (end < 0)
            {
                // No closer found; advance past the opener to allow subsequent matches later in the text.
                position = start + Grammar.Bold.Length;
                continue;
            }
            end += afterOpen;

            // Require non-empty content between the opener and closer (i.e., at least one character inside the bold segment).
            if (end > start + Grammar.Bold.Length)
            {
                // Index immediately after the closing bold token.
                int after = end + Grammar.Bold.Length;
                // Slice of text following the bold segment; used to detect newline sequences.
                ReadOnlySpan<char> tail = after <= span.Length ? span[after..] : [];
                // Bold header followed by newline(s) — split BEFORE the bold segment.
                foreach (string nl in Grammar._paragraphBoundaries)
                {
                    if (tail.StartsWith(nl, StringComparison.Ordinal))
                    {
                        // Found a boundary: return the index of the bold opener so callers split BEFORE it.
                        return start;
                    }
                }
            }

            // Advance scan position to just after the bold closer and continue searching.
            position = end + Grammar.Bold.Length;
        }
        return -1;
    }
}
