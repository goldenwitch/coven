// SPDX-License-Identifier: BUSL-1.1
using Coven.Core.Streaming;

namespace Coven.Agents.Gemini;

/// <summary>
/// Shatters AgentThought outputs on the first matched "summary marker":
/// any bold Markdown segment ("**...**") immediately followed by a newline sequence
/// ("\n\n", "\r\n\r\n", or "\r\n").
/// </summary>
public sealed class AgentThoughtSummaryShatterPolicy : IShatterPolicy<AgentEntry>
{
    private static class Grammar
    {
        public const string Bold = "**";
        public static readonly string[] _paragraphBoundaries = ["\r\n\r\n", "\n\n", "\r\n"];
    }

    /// <summary>
    /// Splits an <see cref="AgentThought"/> into up to two entries at the first detected summary marker boundary.
    /// </summary>
    /// <param name="entry">The source agent entry.</param>
    /// <returns>Zero or more <see cref="AgentEntry"/> instances with at most two thought parts.</returns>
    public IEnumerable<AgentEntry> Shatter(AgentEntry entry)
    {
        if (entry is not AgentThought thought || string.IsNullOrEmpty(thought.Text))
        {
            yield break;
        }

        string text = thought.Text;
        int splitIndex = IndexOfSummaryBoundary(text);
        if (splitIndex < 0)
        {
            yield break;
        }

        string first = text[..splitIndex];
        string second = text[splitIndex..];

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
            int start = span[position..].IndexOf(Grammar.Bold);
            if (start < 0)
            {
                return -1;
            }
            start += position;

            int afterOpen = start + Grammar.Bold.Length;
            if (afterOpen >= span.Length)
            {
                return -1;
            }

            int end = span[afterOpen..].IndexOf(Grammar.Bold);
            if (end < 0)
            {
                position = start + Grammar.Bold.Length;
                continue;
            }
            end += afterOpen;

            if (end > start + Grammar.Bold.Length)
            {
                int after = end + Grammar.Bold.Length;
                ReadOnlySpan<char> tail = after <= span.Length ? span[after..] : [];
                foreach (string nl in Grammar._paragraphBoundaries)
                {
                    if (tail.StartsWith(nl, StringComparison.Ordinal))
                    {
                        return start;
                    }
                }
            }

            position = end + Grammar.Bold.Length;
        }
        return -1;
    }
}
