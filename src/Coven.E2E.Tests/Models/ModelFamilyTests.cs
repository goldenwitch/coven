// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Xunit;

namespace Coven.E2E.Tests.Models;

/// <summary>
/// Tests for family-pattern model classification — the mechanism that lets an unreleased
/// model be classified correctly without a code change.
/// </summary>
public sealed class ModelFamilyTests
{
    /// <summary>
    /// A model that does not exist yet still resolves to its family, because rules match on
    /// family prefixes rather than exact identifiers. This is the whole point of the design.
    /// </summary>
    [Theory]
    // Current, undated IDs — the seed default lives in this shape.
    [InlineData("claude-opus-5", "claude-opus")]
    [InlineData("claude-sonnet-5", "claude-sonnet")]
    [InlineData("claude-haiku-4-5", "claude-haiku")]
    // Unreleased successors.
    [InlineData("claude-sonnet-9-20991231", "claude-sonnet")]
    [InlineData("claude-opus-7-20300101", "claude-opus")]
    [InlineData("claude-haiku-5-20280101", "claude-haiku")]
    [InlineData("gpt-9-turbo", "gpt")]
    [InlineData("o7-mini", "o-series")]
    [InlineData("gemini-4.0-pro", "gemini-pro")]
    [InlineData("gemini-4.0-flash", "gemini-flash")]
    // Current Gemini ids, as returned by the API with the "models/" prefix stripped.
    [InlineData("gemini-2.0-flash", "gemini-flash")]
    [InlineData("gemini-3.0-pro", "gemini-pro")]
    public void UnreleasedModelsResolveToTheirFamily(string modelId, string expectedFamily)
    {
        ModelFamilyRule rule = ModelFamilies.Resolve(modelId);
        Assert.Equal(expectedFamily, rule.Family);
        Assert.True(rule.IsChatModel);
    }

    /// <summary>
    /// An unrecognized model is surfaced in the "other" group rather than hidden. Hiding it
    /// would mean a user could not select something the application declined to show.
    /// </summary>
    [Fact]
    public void UnknownModelsRemainSelectable()
    {
        ModelFamilyRule rule = ModelFamilies.Resolve("brand-new-provider-model-v1");

        Assert.Equal(ModelFamilies.OtherFamily, rule.Family);
        Assert.True(rule.IsChatModel);
        Assert.True(rule.Capabilities.HasFlag(ModelCapabilities.Streaming));
    }

    /// <summary>
    /// OpenAI's list endpoint mixes non-chat models in with chat models, and the payload does
    /// not distinguish them. These must not appear in a chat model picker.
    /// </summary>
    [Theory]
    [InlineData("text-embedding-3-large")]
    [InlineData("tts-1-hd")]
    [InlineData("whisper-1")]
    [InlineData("dall-e-3")]
    [InlineData("omni-moderation-latest")]
    [InlineData("davinci-002")]
    [InlineData("gpt-4o-realtime-preview")]
    [InlineData("gpt-4o-transcribe")]
    public void NonChatModelsAreExcluded(string modelId)
    {
        Assert.False(ModelFamilies.Resolve(modelId).IsChatModel);
    }

    /// <summary>Ordinary chat models survive the non-chat filtering.</summary>
    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-5-2025-08-07")]
    [InlineData("chatgpt-4o-latest")]
    [InlineData("o3-mini")]
    public void ChatModelsSurviveFiltering(string modelId)
    {
        Assert.True(ModelFamilies.Resolve(modelId).IsChatModel);
    }

    /// <summary>A user-supplied override wins over the built-in rules, with no rebuild.</summary>
    [Fact]
    public void OverridesTakePrecedence()
    {
        ModelFamilyRule[] overrides =
        [
            new("gpt-*", "custom-family", ModelCapabilities.Streaming)
        ];

        ModelFamilyRule rule = ModelFamilies.Resolve("gpt-4o", overrides);

        Assert.Equal("custom-family", rule.Family);
    }

    /// <summary>Glob matching is case-insensitive and anchored at both ends.</summary>
    [Theory]
    [InlineData("gpt-4o", "gpt-*", true)]
    [InlineData("GPT-4O", "gpt-*", true)]
    [InlineData("my-gpt-4o", "gpt-*", false)]
    [InlineData("claude-3-5-sonnet-latest", "claude-*sonnet*", true)]
    [InlineData("claude-3-5-haiku-latest", "claude-*sonnet*", false)]
    [InlineData("model.gguf", "*.gguf", true)]
    [InlineData("exact", "exact", true)]
    [InlineData("exact-plus", "exact", false)]
    [InlineData("o7-mini", "o#*", true)]
    [InlineData("omni-moderation", "o#*", false)]
    [InlineData("o7", "o#*", true)]
    public void GlobMatchingBehaves(string value, string pattern, bool expected)
    {
        Assert.Equal(expected, ModelFamilies.Matches(value, pattern));
    }
}
