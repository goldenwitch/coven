// SPDX-License-Identifier: BUSL-1.1

using System.Text;
using System.Text.RegularExpressions;

namespace Coven.Ui.Desktop.HuggingFace;

/// <summary>
/// Extracts a short prose summary from a Hugging Face model card.
/// </summary>
/// <remarks>
/// A model card is a full README: YAML frontmatter, a title, badge images, build instructions,
/// tables and screens of benchmarks. None of that belongs in a one-paragraph summary, so this
/// walks past the furniture and takes the first run of actual sentences.
/// </remarks>
internal static partial class ModelCardSummary
{
    private const int MaxLength = 400;

    /// <summary>Matches an HTML tag, which model cards use freely for badges and layout.</summary>
    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();

    /// <summary>Matches a markdown link, capturing the text so the URL can be dropped.</summary>
    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    /// <summary>Collapses runs of whitespace left behind by the strippers above.</summary>
    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessWhitespace();

    /// <summary>
    /// Pulls the first prose paragraph out of a model card.
    /// </summary>
    /// <param name="markdown">Raw README contents.</param>
    /// <returns>A trimmed summary, or an empty string when the card has no usable prose.</returns>
    public static string Extract(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        string[] lines = StripFrontMatter(markdown).Split('\n');

        // Prefer the paragraph under an introductory heading. Without this the first prose in
        // the file wins, and on many cards that is quantization or build boilerplate that
        // happens to precede the actual description.
        int intro = FindIntroHeading(lines);
        if (intro >= 0)
        {
            // Bounded by the next heading: a section can be all bullets or a table, and
            // running past it lands in an unrelated part of the card.
            string preferred = CleanInline(CollectParagraph(lines, intro + 1, stopAtHeading: true).Text);
            if (preferred.Length > 0)
            {
                return Truncate(preferred);
            }
        }

        return Truncate(FirstSubstantialParagraph(lines));
    }

    /// <summary>
    /// Returns the first prose paragraph of real length, falling back to the first of any
    /// length.
    /// </summary>
    /// <remarks>
    /// Cards routinely open with a one-line aside — a build flag, a quantization note, a
    /// pointer to a guide — before the paragraph that actually describes the model. Length is
    /// a crude signal but a reliable one: the description is a paragraph, the asides are not.
    /// </remarks>
    private static string FirstSubstantialParagraph(string[] lines)
    {
        const int Substantial = 120;

        string? firstAny = null;
        int cursor = 0;

        while (cursor < lines.Length)
        {
            (string text, int next) = CollectParagraph(lines, cursor, stopAtHeading: false);
            if (text.Length == 0)
            {
                break;
            }

            string cleaned = CleanInline(text);
            firstAny ??= cleaned;

            if (cleaned.Length >= Substantial)
            {
                return cleaned;
            }

            // CollectParagraph always advances, so this terminates.
            cursor = Math.Max(next, cursor + 1);
        }

        return firstAny ?? string.Empty;
    }

    /// <summary>
    /// Finds a heading that introduces the model, such as <c>## Introduction</c>.
    /// </summary>
    /// <returns>The heading's line index, or -1 when the card has none.</returns>
    private static int FindIntroHeading(string[] lines)
    {
        string[] wanted =
        [
            "introduction", "description", "overview", "about",
            "summary", "model summary", "model description", "model card",
            "model details", "model overview", "model information", "model info"
        ];

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith('#'))
            {
                continue;
            }

            string heading = line.TrimStart('#').Trim().TrimEnd(':').Trim();
            foreach (string candidate in wanted)
            {
                if (heading.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Collects the first prose paragraph at or after <paramref name="start"/>.</summary>
    /// <param name="lines">Card body, split into lines.</param>
    /// <param name="start">Index to begin scanning from.</param>
    /// <param name="stopAtHeading">
    /// Whether a heading ends the search even before any prose is found, confining the scan to
    /// one section.
    /// </param>
    /// <returns>The paragraph, and the line index scanning stopped at.</returns>
    private static (string Text, int Next) CollectParagraph(string[] lines, int start, bool stopAtHeading)
    {
        StringBuilder paragraph = new();
        bool insideCodeFence = false;
        int i = start;

        for (; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (stopAtHeading && !insideCodeFence && line.StartsWith('#'))
            {
                break;
            }

            // Fenced code has to be tracked rather than skipped line by line: several cards
            // open with build instructions, and treating their contents as prose yields a
            // summary made of apt-get commands.
            if (line.StartsWith("```", StringComparison.Ordinal) ||
                line.StartsWith("~~~", StringComparison.Ordinal))
            {
                insideCodeFence = !insideCodeFence;

                if (paragraph.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (insideCodeFence)
            {
                continue;
            }

            if (line.Length == 0)
            {
                // A blank line ends the paragraph — but only once something was collected,
                // so the gaps between the title and the prose are skipped.
                if (paragraph.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (IsFurniture(line))
            {
                // Skipping mid-paragraph would splice unrelated sentences together.
                if (paragraph.Length > 0)
                {
                    break;
                }

                continue;
            }

            if (paragraph.Length > 0)
            {
                _ = paragraph.Append(' ');
            }

            _ = paragraph.Append(line);
        }

        return (paragraph.ToString(), i + 1);
    }

    /// <summary>Removes the leading YAML metadata block, when present.</summary>
    private static string StripFrontMatter(string markdown)
    {
        string text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart();
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return text;
        }

        int end = text.IndexOf("\n---", StringComparison.Ordinal);
        if (end < 0)
        {
            return text;
        }

        int after = text.IndexOf('\n', end + 1);
        return after < 0 ? string.Empty : text[(after + 1)..];
    }

    /// <summary>Whether a line is structure or decoration rather than prose.</summary>
    private static bool IsFurniture(string line)
    {
        if (line.StartsWith('#') || line.StartsWith('|') || line.StartsWith('>'))
        {
            return true;
        }

        // Badge rows, logos and raw HTML blocks.
        if (line.StartsWith('<') || line.StartsWith("![", StringComparison.Ordinal) ||
            line.StartsWith("[!", StringComparison.Ordinal))
        {
            return true;
        }

        // Horizontal rules.
        if (line.StartsWith("---", StringComparison.Ordinal) ||
            line.StartsWith("***", StringComparison.Ordinal))
        {
            return true;
        }

        // A bullet or numbered item: a list before any prose is a feature dump, not a summary.
        if (line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("* ", StringComparison.Ordinal) ||
            line.StartsWith("+ ", StringComparison.Ordinal))
        {
            return true;
        }

        // A line that is purely a markdown link — usually a badge row.
        return line.StartsWith('[') && line.EndsWith(')') && !line.Contains(". ", StringComparison.Ordinal);
    }

    /// <summary>Reduces inline markup to the text a reader actually wants.</summary>
    private static string CleanInline(string text)
    {
        // Links first: the URL is noise, but the link text is often the subject of the
        // sentence and cannot simply be dropped with it.
        string cleaned = MarkdownLink().Replace(text, "$1");
        cleaned = HtmlTag().Replace(cleaned, string.Empty);

        // Underscores are left alone. They are markdown emphasis in principle, but in these
        // cards they are far more often part of a quantization name, and stripping them turns
        // Q3_K_XL into Q3KXL.
        StringBuilder stripped = new(cleaned.Length);
        foreach (char c in cleaned)
        {
            if (c is '*' or '`')
            {
                continue;
            }

            _ = stripped.Append(c);
        }

        return ExcessWhitespace().Replace(stripped.ToString(), " ").Trim();
    }

    /// <summary>Truncates on a word boundary so the summary does not end mid-word.</summary>
    private static string Truncate(string text)
    {
        // A paragraph that introduces a list ends on a colon, which reads as truncation.
        string trimmed = text.TrimEnd(':', ' ');

        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        int cut = trimmed.LastIndexOf(' ', Math.Min(MaxLength, trimmed.Length - 1));
        if (cut <= 0)
        {
            cut = MaxLength;
        }

        return string.Concat(trimmed.AsSpan(0, cut).TrimEnd(",.;:".AsSpan()), "…");
    }
}
