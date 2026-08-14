// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat.Ui;
using Coven.Ui.Desktop;
using Coven.Ui.Desktop.Settings;
using Xunit;

namespace Coven.E2E.Tests.Ui;

/// <summary>
/// Tests for the rule deciding whether a settings change can be applied in place.
/// </summary>
/// <remarks>
/// This is user-visible in a way most configuration logic is not: journal hydration is not
/// implemented, so a rebuild discards the conversation. Getting this wrong silently throws
/// away someone's transcript.
/// </remarks>
public sealed class SessionRebuildTests
{
    private static AppSettings Baseline() => new()
    {
        Provider = AgentProvider.Anthropic,
        AnthropicApiKey = "key-a",
        OpenAIApiKey = "key-b",
        GeminiApiKey = "key-c",
        AnthropicModel = "claude-opus-5",
        OpenAIModel = "gpt-5-2025-08-07",
        GeminiModel = "gemini-2.0-flash",
        // Registration only requires a non-empty path; weights load when the gateway connects.
        LocalModel = Path.Combine(Path.GetTempPath(), "coven-test-model.gguf"),
        ModelsDirectory = Path.GetTempPath(),
        HuggingFaceToken = "hf_token",
        SystemPrompt = "prompt"
    };

    /// <summary>
    /// Anthropic exposes a settable model on a config the gateway reads per request, so the
    /// conversation survives a model change.
    /// </summary>
    [Fact]
    public void AnthropicModelChangeAppliesInPlace()
    {
        AppSettings current = Baseline();
        AppSettings updated = current.Clone();
        updated.AnthropicModel = "claude-opus-4-20250101";

        Assert.False(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>
    /// OpenAI's config is a record with init-only properties, so nothing can be changed after
    /// registration — including the model.
    /// </summary>
    [Fact]
    public void OpenAIModelChangeRequiresRebuild()
    {
        AppSettings current = Baseline();
        current.Provider = AgentProvider.OpenAI;

        AppSettings updated = current.Clone();
        updated.OpenAIModel = "gpt-4o";

        Assert.True(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>Gemini's config is init-only too, so a model change rebuilds.</summary>
    [Fact]
    public void GeminiModelChangeRequiresRebuild()
    {
        AppSettings current = Baseline();
        current.Provider = AgentProvider.Gemini;

        AppSettings updated = current.Clone();
        updated.GeminiModel = "gemini-3.0-pro";

        Assert.True(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>The API key is baked into the gateway's HttpClient headers at construction.</summary>
    [Fact]
    public void ApiKeyChangeRequiresRebuild()
    {
        AppSettings current = Baseline();
        AppSettings updated = current.Clone();
        updated.AnthropicApiKey = "key-rotated";

        Assert.True(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>The provider determines which agent daemon is registered.</summary>
    [Fact]
    public void ProviderChangeRequiresRebuild()
    {
        AppSettings current = Baseline();
        AppSettings updated = current.Clone();
        updated.Provider = AgentProvider.OpenAI;

        Assert.True(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>The system prompt is captured in the registered config.</summary>
    [Fact]
    public void SystemPromptChangeRequiresRebuild()
    {
        AppSettings current = Baseline();
        AppSettings updated = current.Clone();
        updated.SystemPrompt = "a different prompt";

        Assert.True(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>An unrelated provider's key can change without disturbing the running session.</summary>
    [Fact]
    public void InactiveProviderKeyChangeDoesNotRebuild()
    {
        AppSettings current = Baseline();
        AppSettings updated = current.Clone();
        updated.OpenAIApiKey = "key-b-rotated";

        Assert.False(SessionManager.RequiresRebuild(current, updated));
    }

    /// <summary>No change at all needs no rebuild.</summary>
    [Fact]
    public void IdenticalSettingsDoNotRebuild()
    {
        AppSettings current = Baseline();
        Assert.False(SessionManager.RequiresRebuild(current, current.Clone()));
    }

    /// <summary>
    /// Every provider the options window offers builds a covenant that passes validation.
    /// Without this, a provider selection that cannot start is only discovered at runtime,
    /// after the previous session has already been torn down.
    /// </summary>
    [Theory]
    [InlineData("Anthropic")]
    [InlineData("OpenAI")]
    [InlineData("Gemini")]
    [InlineData("Local")]
    public async Task EveryProviderBuildsAValidCovenant(string providerName)
    {
        AgentProvider provider = Enum.Parse<AgentProvider>(providerName);

        AppSettings settings = Baseline();
        settings.Provider = provider;

        UiChannel channel = new();
        SessionContext context = new();

        // Create builds the host and validates covenant routes; it does not start the ritual.
        await using CovenSession session = CovenSession.Create(settings, channel, context);

        Assert.Equal(provider, session.Provider);
    }

    /// <summary>
    /// Only Anthropic can change model in place, matching what
    /// <see cref="SessionManager.RequiresRebuild"/> promises the user.
    /// </summary>
    [Theory]
    [InlineData("Anthropic", true)]
    [InlineData("OpenAI", false)]
    [InlineData("Gemini", false)]
    [InlineData("Local", false)]
    public async Task HotModelChangeMatchesTheRebuildRule(string providerName, bool expectedHot)
    {
        AppSettings settings = Baseline();
        settings.Provider = Enum.Parse<AgentProvider>(providerName);

        UiChannel channel = new();
        SessionContext context = new();

        await using CovenSession session = CovenSession.Create(settings, channel, context);

        Assert.Equal(expectedHot, session.TryApplyModel("some-other-model"));
    }

    /// <summary>Round-trips through the store preserve every field, keys included.</summary>
    [Fact]
    public void SettingsRoundTripThroughTheStore()
    {
        string path = Path.Combine(Path.GetTempPath(), $"coven-settings-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore store = new(path);
            AppSettings original = Baseline();
            store.Save(original);

            AppSettings loaded = store.Load();

            Assert.Equal(original.Provider, loaded.Provider);
            Assert.Equal(original.AnthropicApiKey, loaded.AnthropicApiKey);
            Assert.Equal(original.OpenAIApiKey, loaded.OpenAIApiKey);
            Assert.Equal(original.GeminiApiKey, loaded.GeminiApiKey);
            Assert.Equal(original.AnthropicModel, loaded.AnthropicModel);
            Assert.Equal(original.OpenAIModel, loaded.OpenAIModel);
            Assert.Equal(original.GeminiModel, loaded.GeminiModel);
            Assert.Equal(original.LocalModel, loaded.LocalModel);
            Assert.Equal(original.ModelsDirectory, loaded.ModelsDirectory);
            Assert.Equal(original.HuggingFaceToken, loaded.HuggingFaceToken);
            Assert.Equal(original.SystemPrompt, loaded.SystemPrompt);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Keys are not written to disk in readable form on platforms that can encrypt.</summary>
    [Fact]
    public void StoredKeysAreNotPlainTextWhenEncryptionIsAvailable()
    {
        if (!SecretProtector.IsEncrypted)
        {
            // Plain-text storage is the documented fallback; nothing to assert.
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"coven-settings-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore store = new(path);
            AppSettings settings = Baseline();
            settings.AnthropicApiKey = "sk-ant-super-secret-value";
            store.Save(settings);

            string raw = File.ReadAllText(path);
            Assert.DoesNotContain("sk-ant-super-secret-value", raw, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
