// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Coven.Agents;

/// <summary>
/// A rule mapping a model-id pattern to a family and its inferred capabilities.
/// </summary>
/// <param name="Pattern">
/// Case-insensitive glob over the model id: <c>*</c> matches any run of characters and
/// <c>#</c> matches a single digit.
/// </param>
/// <param name="Family">Family assigned to matching models.</param>
/// <param name="Capabilities">Capabilities assigned to matching models.</param>
/// <param name="IsChatModel">Whether matching models can hold a conversation.</param>
public sealed record ModelFamilyRule(
    string Pattern,
    string Family,
    ModelCapabilities Capabilities,
    bool IsChatModel = true);

/// <summary>
/// Resolves a model identifier to a family and inferred capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Rules match on family patterns rather than exact identifiers, so an unreleased model in a
/// known family is classified correctly without a code change — <c>gpt-6-mini</c> matches
/// <c>gpt-*</c> on the day it ships.
/// </para>
/// <para>
/// Unmatched models are assigned the <see cref="OtherFamily"/> family rather than discarded.
/// Hiding an unrecognized model would be the worse failure: a user could not select something
/// the application declined to show.
/// </para>
/// </remarks>
public static class ModelFamilies
{
    /// <summary>Family assigned to models no rule matches.</summary>
    public const string OtherFamily = "other";

    private const ModelCapabilities ConservativeDefault = ModelCapabilities.Streaming | ModelCapabilities.Tools;

    private static readonly ModelFamilyRule[] _defaultRules =
    [
        // ── Anthropic ──
        new("claude-*opus*", "claude-opus", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision | ModelCapabilities.Thinking),
        new("claude-*sonnet*", "claude-sonnet", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision | ModelCapabilities.Thinking),
        new("claude-*haiku*", "claude-haiku", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision),
        new("claude-*", "claude", ConservativeDefault | ModelCapabilities.Vision),

        // ── OpenAI: non-chat families are matched first so the chat rules stay simple ──
        new("text-embedding-*", "embedding", ModelCapabilities.None, IsChatModel: false),
        new("tts-*", "audio", ModelCapabilities.None, IsChatModel: false),
        new("gpt-*-tts*", "audio", ModelCapabilities.None, IsChatModel: false),
        new("whisper-*", "audio", ModelCapabilities.None, IsChatModel: false),
        new("gpt-*-transcribe*", "audio", ModelCapabilities.None, IsChatModel: false),
        new("dall-e-*", "image", ModelCapabilities.None, IsChatModel: false),
        new("gpt-image-*", "image", ModelCapabilities.None, IsChatModel: false),
        new("*-moderation-*", "moderation", ModelCapabilities.None, IsChatModel: false),
        new("*-realtime*", "realtime", ModelCapabilities.Streaming, IsChatModel: false),
        new("babbage-*", "legacy", ModelCapabilities.None, IsChatModel: false),
        new("davinci-*", "legacy", ModelCapabilities.None, IsChatModel: false),
        new("codex-*", "legacy", ModelCapabilities.None, IsChatModel: false),

        new("gpt-*", "gpt", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision),
        new("chatgpt-*", "gpt", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision),
        // 'o' followed by a digit, so a future o-series release matches without an edit.
        new("o#*", "o-series", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Thinking),

        // ── Google ──
        new("gemini-*pro*", "gemini-pro", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision),
        new("gemini-*flash*", "gemini-flash", ModelCapabilities.Streaming | ModelCapabilities.Tools | ModelCapabilities.Vision),
        new("gemini-*", "gemini", ConservativeDefault | ModelCapabilities.Vision),

        // ── Local ──
        new("*.gguf", "local", ModelCapabilities.Streaming)
    ];

    /// <summary>The built-in rules, in match order.</summary>
    public static IReadOnlyList<ModelFamilyRule> DefaultRules => _defaultRules;

    /// <summary>
    /// Resolves a model identifier against the supplied rules, first match wins.
    /// </summary>
    /// <param name="modelId">The provider-native model identifier.</param>
    /// <param name="overrides">
    /// Optional rules evaluated ahead of the built-ins, letting a user correct a
    /// misclassification without a rebuild.
    /// </param>
    /// <returns>The matching rule, or a conservative fallback in the <see cref="OtherFamily"/> family.</returns>
    public static ModelFamilyRule Resolve(string modelId, IReadOnlyList<ModelFamilyRule>? overrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (overrides is not null)
        {
            foreach (ModelFamilyRule rule in overrides)
            {
                if (Matches(modelId, rule.Pattern))
                {
                    return rule;
                }
            }
        }

        foreach (ModelFamilyRule rule in _defaultRules)
        {
            if (Matches(modelId, rule.Pattern))
            {
                return rule;
            }
        }

        return new ModelFamilyRule(modelId, OtherFamily, ConservativeDefault);
    }

    /// <summary>
    /// Case-insensitive whole-string match where <c>*</c> matches any run of characters and
    /// <c>#</c> matches a single digit. Every other character is literal.
    /// </summary>
    public static bool Matches(string value, string pattern)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(pattern);

        return _matchers.GetOrAdd(pattern, Compile).IsMatch(value);
    }

    /// <summary>
    /// Patterns come from a fixed table and from user overrides, so the set is small and
    /// long-lived; compiling each one once is cheaper than rebuilding it per model per
    /// listing.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex> _matchers = new(StringComparer.Ordinal);

    /// <summary>
    /// Translates a pattern into an anchored regular expression.
    /// </summary>
    /// <remarks>
    /// The matching itself is left to <see cref="Regex"/> rather than hand-written: a
    /// wildcard matcher is deceptively easy to get subtly wrong, and a naive one backtracks
    /// exponentially on patterns like <c>*a*a*a*</c>.
    /// </remarks>
    private static Regex Compile(string pattern)
    {
        StringBuilder expression = new("^");
        foreach (char token in pattern)
        {
            _ = token switch
            {
                '*' => expression.Append(".*"),
                '#' => expression.Append("[0-9]"),
                _ => expression.Append(Regex.Escape(token.ToString())),
            };
        }

        return new Regex(
            expression.Append('$').ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
