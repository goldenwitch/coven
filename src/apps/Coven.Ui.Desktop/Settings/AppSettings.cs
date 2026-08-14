// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.LLamaSharp;

namespace Coven.Ui.Desktop.Settings;

/// <summary>
/// Agent providers the application can run.
/// </summary>
internal enum AgentProvider
{
    /// <summary>Anthropic Claude.</summary>
    Anthropic = 0,

    /// <summary>OpenAI.</summary>
    OpenAI = 1,

    /// <summary>Google Gemini.</summary>
    Gemini = 2,

    /// <summary>A GGUF model running locally through LLamaSharp.</summary>
    Local = 3
}

/// <summary>
/// User-editable application settings.
/// </summary>
/// <remarks>
/// Keys and models are held per provider so switching back and forth does not discard
/// credentials the user already entered.
/// </remarks>
internal sealed class AppSettings
{
    /// <summary>
    /// Seed model used on first run, before the catalog has been fetched. Only a starting
    /// point: the options window lists live models and the selection is persisted.
    /// </summary>
    /// <remarks>
    /// Keep this on a current model. The previous seed was a dated Sonnet 4 snapshot that has
    /// since passed its retirement date — a stale seed turns first run into a 404 for anyone
    /// who has not opened Options yet, which is the worst possible moment for it.
    /// </remarks>
    public const string SeedAnthropicModel = "claude-opus-5";

    /// <summary>Seed model used on first run for OpenAI.</summary>
    public const string SeedOpenAIModel = "gpt-5-2025-08-07";

    /// <summary>Seed model used on first run for Gemini.</summary>
    public const string SeedGeminiModel = "gemini-2.0-flash";

    /// <summary>
    /// Default directory for downloaded GGUF models. Chosen under the roaming application
    /// data folder for consistency with the settings file; models are large, so the options
    /// window lets this be moved to another drive.
    /// </summary>
    public static string DefaultModelsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Coven",
        "models");

    /// <summary>Default system prompt.</summary>
    public const string DefaultSystemPrompt =
        "You are a helpful assistant running inside the Coven desktop application.";

    /// <summary>Provider backing the current session.</summary>
    public AgentProvider Provider { get; set; } = AgentProvider.Anthropic;

    /// <summary>Anthropic API key.</summary>
    public string AnthropicApiKey { get; set; } = string.Empty;

    /// <summary>OpenAI API key.</summary>
    public string OpenAIApiKey { get; set; } = string.Empty;

    /// <summary>Gemini API key.</summary>
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>Selected Anthropic model identifier.</summary>
    public string AnthropicModel { get; set; } = SeedAnthropicModel;

    /// <summary>Selected OpenAI model identifier.</summary>
    public string OpenAIModel { get; set; } = SeedOpenAIModel;

    /// <summary>Selected Gemini model identifier.</summary>
    public string GeminiModel { get; set; } = SeedGeminiModel;

    /// <summary>Full path to the selected local GGUF file. Empty until one is chosen.</summary>
    public string LocalModel { get; set; } = string.Empty;

    /// <summary>Directory scanned for local GGUF models and used as the download target.</summary>
    public string ModelsDirectory { get; set; } = DefaultModelsDirectory;

    /// <summary>
    /// Optional Hugging Face access token. Only needed for gated or private repositories;
    /// public GGUF downloads work without one.
    /// </summary>
    public string HuggingFaceToken { get; set; } = string.Empty;

    /// <summary>System prompt applied to the session.</summary>
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    /// <summary>API key for the active provider.</summary>
    public string ActiveApiKey => ApiKeyFor(Provider);

    /// <summary>Model for the active provider.</summary>
    public string ActiveModel => ModelFor(Provider);

    /// <summary>Whether the active provider is a hosted API that authenticates with a key.</summary>
    public bool ActiveProviderUsesApiKey => Provider != AgentProvider.Local;

    /// <summary>
    /// Whether the active provider has everything it needs to start a session. Hosted
    /// providers need a key; the local provider needs a model file that actually exists on
    /// disk, which is a different question and cannot be answered by a key check.
    /// </summary>
    public bool IsConfigured => Provider == AgentProvider.Local
        ? !string.IsNullOrWhiteSpace(LocalModel)
            && File.Exists(LocalModel)
            && !GgufShards.TryFindProblem(LocalModel, out _)
        : !string.IsNullOrWhiteSpace(ActiveApiKey);

    /// <summary>Explains what is missing when <see cref="IsConfigured"/> is false.</summary>
    public string ConfigurationHint
    {
        get
        {
            if (Provider != AgentProvider.Local)
            {
                return $"No API key set for {Provider}. Open Options to add one.";
            }

            if (string.IsNullOrWhiteSpace(LocalModel))
            {
                return "No local model selected. Open Options to download or choose a GGUF file.";
            }

            if (!File.Exists(LocalModel))
            {
                return $"The selected local model no longer exists at {LocalModel}. Open Options to choose another.";
            }

            // A selected file can exist and still be unloadable: split models are published
            // across numbered parts, and only the first one — with all the others present —
            // can be opened.
            return GgufShards.TryFindProblem(LocalModel, out string problem)
                ? problem
                : $"The selected local model at {LocalModel} cannot be used. Open Options to choose another.";
        }
    }

    /// <summary>API key for a specific provider.</summary>
    public string ApiKeyFor(AgentProvider provider) => provider switch
    {
        AgentProvider.Anthropic => AnthropicApiKey,
        AgentProvider.OpenAI => OpenAIApiKey,
        AgentProvider.Gemini => GeminiApiKey,
        _ => string.Empty
    };

    /// <summary>Model for a specific provider.</summary>
    public string ModelFor(AgentProvider provider) => provider switch
    {
        AgentProvider.Anthropic => AnthropicModel,
        AgentProvider.OpenAI => OpenAIModel,
        AgentProvider.Gemini => GeminiModel,
        AgentProvider.Local => LocalModel,
        _ => string.Empty
    };

    /// <summary>Sets the API key for a specific provider.</summary>
    public void SetApiKey(AgentProvider provider, string apiKey)
    {
        switch (provider)
        {
            case AgentProvider.Anthropic:
                AnthropicApiKey = apiKey;
                break;
            case AgentProvider.OpenAI:
                OpenAIApiKey = apiKey;
                break;
            case AgentProvider.Gemini:
                GeminiApiKey = apiKey;
                break;
            default:
                break;
        }
    }

    /// <summary>Sets the model for a specific provider.</summary>
    public void SetModel(AgentProvider provider, string model)
    {
        switch (provider)
        {
            case AgentProvider.Anthropic:
                AnthropicModel = model;
                break;
            case AgentProvider.OpenAI:
                OpenAIModel = model;
                break;
            case AgentProvider.Gemini:
                GeminiModel = model;
                break;
            case AgentProvider.Local:
                LocalModel = model;
                break;
            default:
                break;
        }
    }

    /// <summary>Creates an independent copy, so an options dialog can edit without committing.</summary>
    public AppSettings Clone() => new()
    {
        Provider = Provider,
        AnthropicApiKey = AnthropicApiKey,
        OpenAIApiKey = OpenAIApiKey,
        GeminiApiKey = GeminiApiKey,
        AnthropicModel = AnthropicModel,
        OpenAIModel = OpenAIModel,
        GeminiModel = GeminiModel,
        LocalModel = LocalModel,
        ModelsDirectory = ModelsDirectory,
        HuggingFaceToken = HuggingFaceToken,
        SystemPrompt = SystemPrompt
    };

    /// <summary>
    /// Applies environment variables to any field the user has not set, keeping first-run
    /// behaviour identical to the toys and samples.
    /// </summary>
    public void ApplyEnvironmentFallbacks()
    {
        if (string.IsNullOrWhiteSpace(AnthropicApiKey))
        {
            AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(OpenAIApiKey))
        {
            OpenAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                ?? string.Empty;
        }

        string? claudeModel = Environment.GetEnvironmentVariable("CLAUDE_MODEL");
        if (!string.IsNullOrWhiteSpace(claudeModel))
        {
            AnthropicModel = claudeModel;
        }

        string? openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (!string.IsNullOrWhiteSpace(openAiModel))
        {
            OpenAIModel = openAiModel;
        }

        string? geminiModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        if (!string.IsNullOrWhiteSpace(geminiModel))
        {
            GeminiModel = geminiModel;
        }

        if (string.IsNullOrWhiteSpace(HuggingFaceToken))
        {
            HuggingFaceToken = Environment.GetEnvironmentVariable("HF_TOKEN")
                ?? Environment.GetEnvironmentVariable("HUGGING_FACE_HUB_TOKEN")
                ?? string.Empty;
        }

        string? modelsDirectory = Environment.GetEnvironmentVariable("COVEN_MODELS_DIR");
        if (!string.IsNullOrWhiteSpace(modelsDirectory))
        {
            ModelsDirectory = modelsDirectory;
        }

        string? systemPrompt = Environment.GetEnvironmentVariable("COVEN_SYSTEM_PROMPT");
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            SystemPrompt = systemPrompt;
        }
    }
}
