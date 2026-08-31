// SPDX-License-Identifier: BUSL-1.1

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Agents.Gemini;
using Coven.Agents.LLamaSharp;
using Coven.Agents.OpenAI;
using Coven.Ui.Desktop.Local;
using Coven.Ui.Desktop.Settings;

namespace Coven.Ui.Desktop.ViewModels;

/// <summary>
/// Backs the options window: provider choice, credentials, and model selection.
/// </summary>
/// <remarks>
/// The local provider changes the shape of this dialog rather than just its values — there is
/// no API key, the model is a file on disk, and models are acquired by download rather than
/// by being listed. <see cref="IsLocalProvider"/> drives that switch in the view.
/// </remarks>
internal sealed partial class OptionsViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _original;
    private readonly AppSettings _working;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CancellationTokenSource _cts = new();

    private bool _suppressProviderSync;
    private bool _disposed;

    public OptionsViewModel(AppSettings settings, string settingsPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);

        _original = settings;
        _working = settings.Clone();
        SettingsPath = settingsPath;
        SecretsAreEncrypted = SecretProtector.IsEncrypted;

        _suppressProviderSync = true;
        SelectedProvider = _working.Provider;
        ApiKey = _working.ActiveApiKey;
        ModelId = _working.ActiveModel;
        SystemPrompt = _working.SystemPrompt;
        ModelsDirectory = _working.ModelsDirectory;
        HuggingFaceToken = _working.HuggingFaceToken;
        _suppressProviderSync = false;

        StatusText = DefaultStatus();
        UpdateResetWarning();
    }

    /// <summary>Raised when the window should close. <c>true</c> means settings were saved.</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>Opens the Hugging Face browser. Assigned by the view, which owns the dialog.</summary>
    public Func<ModelBrowserViewModel, Task<string?>>? ShowModelBrowser { get; set; }

    /// <summary>Opens a folder picker starting at the given path. Assigned by the view.</summary>
    public Func<string, Task<string?>>? PickFolder { get; set; }

    /// <summary>Providers the application can run.</summary>
    public IReadOnlyList<AgentProvider> Providers { get; } =
        [AgentProvider.Anthropic, AgentProvider.OpenAI, AgentProvider.Gemini, AgentProvider.Local];

    /// <summary>Models discovered for the selected provider.</summary>
    public ObservableCollection<ModelDescriptor> Models { get; } = [];

    /// <summary>Provider to run.</summary>
    [ObservableProperty]
    public partial AgentProvider SelectedProvider { get; set; }

    /// <summary>API key for the selected provider. Unused by the local provider.</summary>
    [ObservableProperty]
    public partial string ApiKey { get; set; }

    /// <summary>
    /// Effective model identifier — a provider model id, or a full path to a GGUF file for the
    /// local provider. Editable so a failed listing never blocks setting a model by hand.
    /// </summary>
    [ObservableProperty]
    public partial string ModelId { get; set; }

    /// <summary>System prompt for the session.</summary>
    [ObservableProperty]
    public partial string SystemPrompt { get; set; }

    /// <summary>Directory scanned for local models and used as the download target.</summary>
    [ObservableProperty]
    public partial string ModelsDirectory { get; set; }

    /// <summary>Optional Hugging Face access token, for gated or private repositories.</summary>
    [ObservableProperty]
    public partial string HuggingFaceToken { get; set; }

    /// <summary>Selection in the discovered-models list.</summary>
    [ObservableProperty]
    public partial ModelDescriptor? SelectedModel { get; set; }

    /// <summary>Single-line status for the options window.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>Whether a catalog fetch or directory scan is in flight.</summary>
    [ObservableProperty]
    public partial bool IsLoadingModels { get; set; }

    /// <summary>Whether saving will discard the current conversation.</summary>
    [ObservableProperty]
    public partial bool WillResetConversation { get; set; }

    /// <summary>Whether the local provider is selected, which reshapes the dialog.</summary>
    [ObservableProperty]
    public partial bool IsLocalProvider { get; set; }

    /// <summary>Inverse of <see cref="IsLocalProvider"/>, for showing API-key fields.</summary>
    [ObservableProperty]
    public partial bool IsHostedProvider { get; set; } = true;

    /// <summary>Which native backend local inference would use on this machine.</summary>
    [ObservableProperty]
    public partial string BackendDescription { get; set; } = "not probed yet";

    /// <summary>Whether stored keys are encrypted at rest on this platform.</summary>
    public bool SecretsAreEncrypted { get; }

    /// <summary>Where settings are written.</summary>
    public string SettingsPath { get; }

    /// <summary>Warning shown when keys are stored unencrypted.</summary>
    public string SecretStorageWarning =>
        $"API keys are stored in plain text at {SettingsPath} (readable by your user account). "
        + "Encryption at rest is not available on this platform.";

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _httpClient.Dispose();
    }

    /// <summary>Produces the edited settings. Only valid after a save.</summary>
    public AppSettings BuildSettings()
    {
        AppSettings result = _working.Clone();
        result.Provider = SelectedProvider;
        result.SetModel(SelectedProvider, ModelId.Trim());
        result.SystemPrompt = SystemPrompt;
        result.ModelsDirectory = ModelsDirectory.Trim();
        result.HuggingFaceToken = HuggingFaceToken.Trim();

        // The local provider has no key; writing one would overwrite nothing meaningful.
        if (SelectedProvider != AgentProvider.Local)
        {
            result.SetApiKey(SelectedProvider, ApiKey.Trim());
        }

        return result;
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        if (SelectedProvider != AgentProvider.Local && string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusText = "Enter an API key first — the provider's model list requires one.";
            return;
        }

        IsLoadingModels = true;
        StatusText = SelectedProvider == AgentProvider.Local ? "Scanning for models…" : "Loading models…";

        try
        {
            IModelCatalog catalog = CreateCatalog(SelectedProvider, ModelsDirectory, _httpClient);
            IReadOnlyList<ModelDescriptor> models = await catalog
                .ListAsync(new ModelCatalogRequest(ApiKey.Trim()), _cts.Token)
                .ConfigureAwait(true);

            Models.Clear();
            foreach (ModelDescriptor model in models)
            {
                Models.Add(model);
            }

            // Keep the current selection visible even when it is no longer listed, so an
            // unknown, retired, or moved model is never silently dropped from the picker.
            if (!string.IsNullOrWhiteSpace(ModelId) &&
                !Models.Any(m => string.Equals(m.Id, ModelId, StringComparison.Ordinal)))
            {
                ModelFamilyRule rule = ModelFamilies.Resolve(ModelId);
                Models.Insert(0, new ModelDescriptor(ModelId, $"{ModelId} (not listed)", rule.Family, null, null, rule.Capabilities));
            }

            SelectedModel = Models.FirstOrDefault(m => string.Equals(m.Id, ModelId, StringComparison.Ordinal));

            StatusText = SelectedProvider == AgentProvider.Local && models.Count == 0
                ? "No GGUF files found. Use Download a model to fetch one from Hugging Face."
                : $"{models.Count} model(s) available.";
        }
        catch (OperationCanceledException)
        {
            // Window closed.
        }
        catch (Exception ex)
        {
            StatusText = $"Could not load models: {ex.Message}";
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    [RelayCommand]
    private async Task BrowseModelsAsync()
    {
        if (ShowModelBrowser is null)
        {
            return;
        }

        using ModelBrowserViewModel browser = new(ModelsDirectory.Trim(), HuggingFaceToken.Trim());
        string? downloaded = await ShowModelBrowser(browser).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(downloaded))
        {
            return;
        }

        // Select what was just downloaded — the reason for downloading it.
        ModelId = downloaded;
        await RefreshModelsAsync().ConfigureAwait(true);
        StatusText = $"Downloaded and selected {Path.GetFileName(downloaded)}.";
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        if (PickFolder is null)
        {
            return;
        }

        string? picked = await PickFolder(ModelsDirectory).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            ModelsDirectory = picked;
        }
    }

    [RelayCommand]
    private void ProbeBackend() => BackendDescription = LocalBackend.EnsureConfigured();

    [RelayCommand]
    private void Save() => CloseRequested?.Invoke(true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    partial void OnSelectedProviderChanged(AgentProvider value)
    {
        IsLocalProvider = value == AgentProvider.Local;
        IsHostedProvider = !IsLocalProvider;

        if (_suppressProviderSync)
        {
            return;
        }

        // Swap to the other provider's stored credentials rather than carrying the current
        // ones across — keys and models are per provider.
        _suppressProviderSync = true;
        ApiKey = _working.ApiKeyFor(value);
        ModelId = _working.ModelFor(value);
        _suppressProviderSync = false;

        Models.Clear();
        SelectedModel = null;
        StatusText = DefaultStatus();
        UpdateResetWarning();
    }

    partial void OnSelectedModelChanged(ModelDescriptor? value)
    {
        if (value is not null && !string.Equals(value.Id, ModelId, StringComparison.Ordinal))
        {
            ModelId = value.Id;
        }
    }

    partial void OnApiKeyChanged(string value) => UpdateResetWarning();

    partial void OnModelIdChanged(string value) => UpdateResetWarning();

    partial void OnSystemPromptChanged(string value) => UpdateResetWarning();

    private string DefaultStatus() => SelectedProvider == AgentProvider.Local
        ? "Scan for local models, or download one from Hugging Face."
        : "Refresh to list models from the provider.";

    private void UpdateResetWarning()
    {
        if (_suppressProviderSync)
        {
            return;
        }

        WillResetConversation = SessionManager.RequiresRebuild(_original, BuildSettings());
    }

    private static IModelCatalog CreateCatalog(AgentProvider provider, string modelsDirectory, HttpClient httpClient) => provider switch
    {
        AgentProvider.Anthropic => new ClaudeModelCatalog(httpClient),
        AgentProvider.OpenAI => new OpenAIModelCatalog(httpClient),
        AgentProvider.Gemini => new GeminiModelCatalog(httpClient),
        AgentProvider.Local => new LocalModelCatalog(modelsDirectory),
        _ => throw new NotSupportedException($"No catalog for provider {provider}.")
    };
}
