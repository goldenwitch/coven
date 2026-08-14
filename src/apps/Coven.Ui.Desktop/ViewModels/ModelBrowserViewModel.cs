// SPDX-License-Identifier: BUSL-1.1

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Coven.Agents.LLamaSharp;
using Coven.Ui.Desktop.HuggingFace;

namespace Coven.Ui.Desktop.ViewModels;

/// <summary>
/// Backs the Hugging Face model browser: search, pick a quantization, download.
/// </summary>
internal sealed partial class ModelBrowserViewModel : ObservableObject, IDisposable
{
    private readonly HuggingFaceClient _client = new();
    private readonly string _modelsDirectory;
    private readonly string? _token;

    private CancellationTokenSource? _downloadCts;
    private bool _disposed;

    public ModelBrowserViewModel(string modelsDirectory, string? token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);

        _modelsDirectory = modelsDirectory;
        _token = token;

        SearchText = string.Empty;
        StatusText = "Search Hugging Face for GGUF models, or press Search to see the most popular.";
    }

    /// <summary>Raised when the window should close. Carries the downloaded path, if any.</summary>
    public event Action<string?>? CloseRequested;

    /// <summary>Repositories matching the current search.</summary>
    public ObservableCollection<HuggingFaceModel> Results { get; } = [];

    /// <summary>
    /// Selectable models in the repository, smallest first. A model published across several
    /// files appears once, not once per part.
    /// </summary>
    public ObservableCollection<HuggingFaceDownload> Files { get; } = [];

    /// <summary>Free-text search query.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; }

    /// <summary>Single-line status for the browser.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>Selected repository.</summary>
    [ObservableProperty]
    public partial HuggingFaceModel? SelectedModel { get; set; }

    /// <summary>Selected model within the repository.</summary>
    [ObservableProperty]
    public partial HuggingFaceDownload? SelectedFile { get; set; }

    /// <summary>Whether a search or file listing is in flight.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Whether a download is in flight.</summary>
    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    /// <summary>Download completion from 0 to 100, for a progress bar.</summary>
    [ObservableProperty]
    public partial double DownloadPercent { get; set; }

    /// <summary>Human-readable download progress, including throughput.</summary>
    [ObservableProperty]
    public partial string DownloadStatus { get; set; } = string.Empty;

    /// <summary>Description and specifications of the selected repository.</summary>
    [ObservableProperty]
    public partial HuggingFaceModelDetail? Detail { get; set; }

    /// <summary>Whether the details request is in flight.</summary>
    [ObservableProperty]
    public partial bool IsLoadingDetail { get; set; }

    /// <summary>Estimated hardware demands of the selected model.</summary>
    [ObservableProperty]
    public partial ModelHardwareProfile? Hardware { get; set; }

    /// <summary>
    /// States plainly whether the selection is a whole model or a set of parts, because
    /// downloading one part of a set produces a file that cannot be loaded.
    /// </summary>
    [ObservableProperty]
    public partial string CompletenessText { get; set; } = string.Empty;

    /// <summary>Memory the selected model is estimated to need, rendered for display.</summary>
    [ObservableProperty]
    public partial string MemoryText { get; set; } = string.Empty;

    /// <summary>Whether a model is selected and its details can be shown.</summary>
    public bool HasSelection => SelectedFile is not null;

    /// <summary>Where downloads are written.</summary>
    public string ModelsDirectory => _modelsDirectory;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _client.Dispose();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        StatusText = "Searching…";
        Results.Clear();
        Files.Clear();

        try
        {
            IReadOnlyList<HuggingFaceModel> models = await _client
                .SearchAsync(SearchText, _token)
                .ConfigureAwait(true);

            foreach (HuggingFaceModel model in models)
            {
                Results.Add(model);
            }

            StatusText = models.Count == 0
                ? "No GGUF repositories matched that search."
                : $"{models.Count} repositor{(models.Count == 1 ? "y" : "ies")} found. Select one to see its files.";
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (SelectedModel is null || SelectedFile is null || IsDownloading)
        {
            return;
        }

        HuggingFaceModel model = SelectedModel;
        HuggingFaceDownload file = SelectedFile;

        // Keep provenance in the layout so two repos publishing the same filename cannot
        // collide, and so it stays obvious later where a file came from.
        string destination = Path.Combine(
            _modelsDirectory,
            Sanitize(model.Owner),
            Sanitize(model.Name));

        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();

        IsDownloading = true;
        DownloadPercent = 0;
        DownloadStatus = "Starting…";
        StatusText = file.IsSplit
            ? $"Downloading {file.DisplayName} — all {file.Parts.Count} parts are required"
            : $"Downloading {file.DisplayName}";

        // Constructed on the UI thread, so its callbacks marshal back automatically.
        Progress<DownloadProgress> progress = new(OnProgress);

        try
        {
            string modelPath = await _client
                .DownloadModelAsync(model.RepoId, file, destination, _token, progress, _downloadCts.Token)
                .ConfigureAwait(true);

            StatusText = $"Downloaded {file.DisplayName}.";
            CloseRequested?.Invoke(modelPath);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled. Partial data is kept, so downloading again resumes.";
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadStatus = string.Empty;
        }
    }

    [RelayCommand]
    private void CancelDownload() => _downloadCts?.Cancel();

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(null);

    private void OnProgress(DownloadProgress p)
    {
        DownloadPercent = (p.Fraction ?? 0) * 100;

        string done = LocalModelCatalog.FormatSize(p.BytesDownloaded);
        string total = p.TotalBytes is long t ? LocalModelCatalog.FormatSize(t) : "unknown";
        string rate = p.BytesPerSecond > 0
            ? $" · {LocalModelCatalog.FormatSize((long)p.BytesPerSecond)}/s"
            : string.Empty;

        DownloadStatus = string.Create(CultureInfo.InvariantCulture, $"{done} of {total}{rate}");
    }

    partial void OnSelectedModelChanged(HuggingFaceModel? value)
    {
        Files.Clear();
        SelectedFile = null;
        Detail = null;

        if (value is not null)
        {
            _ = LoadFilesAsync(value);
            _ = LoadDetailAsync(value);
        }
    }

    partial void OnSelectedFileChanged(HuggingFaceDownload? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        UpdateSelectionSummary();
    }

    partial void OnDetailChanged(HuggingFaceModelDetail? value)
    {
        // The parameter count arrives with the details, and bits-per-weight needs it, so the
        // estimate is recomputed once it lands.
        UpdateSelectionSummary();
    }

    /// <summary>
    /// Recomputes what is said about the current selection: whether it is complete, and what
    /// it will take to run.
    /// </summary>
    private void UpdateSelectionSummary()
    {
        if (SelectedFile is not HuggingFaceDownload selection)
        {
            CompletenessText = string.Empty;
            MemoryText = string.Empty;
            Hardware = null;
            return;
        }

        CompletenessText = selection.IsSplit
            ? $"Complete model, published in {selection.Parts.Count} parts — all of them download together."
            : "Complete model in a single file.";

        long? parameters = Detail?.ParameterCount > 0 ? Detail.ParameterCount : null;
        ModelHardwareProfile profile = ModelHardware.Estimate(selection.TotalBytes, parameters);

        Hardware = profile;
        MemoryText =
            $"About {LocalModelCatalog.FormatSize(profile.RecommendedMemoryBytes)} of memory "
            + $"({LocalModelCatalog.FormatSize(profile.WeightsBytes)} of weights plus working space).";
    }

    private async Task LoadDetailAsync(HuggingFaceModel model)
    {
        IsLoadingDetail = true;

        try
        {
            HuggingFaceModelDetail detail = await _client
                .GetDetailAsync(model.RepoId, _token)
                .ConfigureAwait(true);

            // The selection may have moved on while the request was in flight.
            if (ReferenceEquals(SelectedModel, model))
            {
                Detail = detail;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Details are supplementary: the file list and the download still work without
            // them, so a failure here is left silent rather than replacing the status line.
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }

    private async Task LoadFilesAsync(HuggingFaceModel model)
    {
        IsBusy = true;
        StatusText = $"Loading files in {model.RepoId}…";

        try
        {
            IReadOnlyList<HuggingFaceDownload> files = await _client
                .ListDownloadsAsync(model.RepoId, _token)
                .ConfigureAwait(true);

            // The selection may have moved on while the request was in flight.
            if (!ReferenceEquals(SelectedModel, model))
            {
                return;
            }

            foreach (HuggingFaceDownload file in files)
            {
                Files.Add(file);
            }

            StatusText = files.Count == 0
                ? "That repository lists no GGUF files."
                : $"{files.Count} model(s). Smaller quantizations run faster and need less memory; larger ones answer better.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not list files: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Strips characters that are not valid in a path segment.</summary>
    private static string Sanitize(string segment)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(segment.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
