// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Coven.Ui.Desktop.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON, with API keys protected by
/// <see cref="SecretProtector"/>.
/// </summary>
/// <param name="filePath">Where settings live. Defaults to <see cref="DefaultPath"/>.</param>
internal sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Standard settings location under the user's application data directory.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Coven",
        "settings.json");

    /// <summary>Creates a store at <see cref="DefaultPath"/>.</summary>
    public SettingsStore()
        : this(DefaultPath)
    {
    }

    /// <summary>Full path of the settings file.</summary>
    public string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));

    /// <summary>
    /// Loads settings, falling back to defaults plus environment variables.
    /// A corrupt file is treated as absent rather than fatal — the options window can
    /// rewrite it, and refusing to launch over unreadable preferences helps nobody.
    /// </summary>
    public AppSettings Load()
    {
        AppSettings settings = new();

        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                SettingsDocument? document = JsonSerializer.Deserialize<SettingsDocument>(json, _serializerOptions);

                if (document is not null)
                {
                    settings.Provider = Enum.TryParse(document.Provider, ignoreCase: true, out AgentProvider provider)
                        ? provider
                        : AgentProvider.Anthropic;
                    settings.AnthropicApiKey = SecretProtector.Unprotect(document.AnthropicApiKey);
                    settings.OpenAIApiKey = SecretProtector.Unprotect(document.OpenAIApiKey);
                    settings.GeminiApiKey = SecretProtector.Unprotect(document.GeminiApiKey);
                    settings.AnthropicModel = Or(document.AnthropicModel, AppSettings.SeedAnthropicModel);
                    settings.OpenAIModel = Or(document.OpenAIModel, AppSettings.SeedOpenAIModel);
                    settings.GeminiModel = Or(document.GeminiModel, AppSettings.SeedGeminiModel);
                    settings.LocalModel = document.LocalModel ?? string.Empty;
                    settings.ModelsDirectory = Or(document.ModelsDirectory, AppSettings.DefaultModelsDirectory);
                    settings.HuggingFaceToken = SecretProtector.Unprotect(document.HuggingFaceToken);
                    settings.SystemPrompt = Or(document.SystemPrompt, AppSettings.DefaultSystemPrompt);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall through to defaults.
        }

        settings.ApplyEnvironmentFallbacks();
        return settings;
    }

    /// <summary>
    /// Writes settings to disk, creating the directory if needed.
    /// </summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SettingsDocument document = new()
        {
            Provider = settings.Provider.ToString(),
            AnthropicApiKey = SecretProtector.Protect(settings.AnthropicApiKey),
            OpenAIApiKey = SecretProtector.Protect(settings.OpenAIApiKey),
            GeminiApiKey = SecretProtector.Protect(settings.GeminiApiKey),
            AnthropicModel = settings.AnthropicModel,
            OpenAIModel = settings.OpenAIModel,
            GeminiModel = settings.GeminiModel,
            LocalModel = settings.LocalModel,
            ModelsDirectory = settings.ModelsDirectory,
            // A Hugging Face token is a credential like any other — protect it at rest.
            HuggingFaceToken = SecretProtector.Protect(settings.HuggingFaceToken),
            SystemPrompt = settings.SystemPrompt
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(document, _serializerOptions));
        RestrictToOwner(FilePath);
    }

    /// <summary>
    /// Restricts the settings file to the owner on Unix. On Windows the keys are DPAPI-encrypted,
    /// so file permissions are not what is protecting them.
    /// </summary>
    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best effort.
        }
    }

    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private sealed record SettingsDocument
    {
        public string? Provider { get; init; }
        public string? AnthropicApiKey { get; init; }
        public string? OpenAIApiKey { get; init; }
        public string? GeminiApiKey { get; init; }
        public string? AnthropicModel { get; init; }
        public string? OpenAIModel { get; init; }
        public string? GeminiModel { get; init; }
        public string? LocalModel { get; init; }
        public string? ModelsDirectory { get; init; }
        public string? HuggingFaceToken { get; init; }
        public string? SystemPrompt { get; init; }
    }
}
