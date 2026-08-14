// SPDX-License-Identifier: BUSL-1.1

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coven.Agents.LLamaSharp;

namespace Coven.Ui.Desktop.HuggingFace;

/// <summary>
/// Searches Hugging Face for GGUF repositories and downloads model files.
/// </summary>
/// <remarks>
/// Downloads are multi-gigabyte, so this streams straight to disk, writes to a
/// <c>.partial</c> sidecar, and resumes with a range request. Nothing is buffered in memory
/// and a cancelled download never leaves a file that looks loadable.
/// </remarks>
internal sealed class HuggingFaceClient : IDisposable
{
    private const string ApiBase = "https://huggingface.co/api";
    private const string ResolveBase = "https://huggingface.co";
    private const string PartialSuffix = ".partial";
    private const int SearchLimit = 30;

    private static readonly JsonSerializerOptions _serializerOptions = new();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    /// <summary>Creates a client with its own <see cref="HttpClient"/>.</summary>
    public HuggingFaceClient()
        : this(
            new HttpClient
            {
                // Generous: a slow mirror can stall a large transfer well past the default.
                Timeout = TimeSpan.FromMinutes(30)
            },
            ownsClient: true)
    {
    }

    /// <summary>Creates a client over a caller-supplied <see cref="HttpClient"/>.</summary>
    public HuggingFaceClient(HttpClient httpClient, bool ownsClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Searches for repositories carrying GGUF files, most-downloaded first.
    /// </summary>
    /// <param name="query">Free-text query. Empty returns the most popular GGUF repositories.</param>
    /// <param name="token">Optional access token for gated or private repositories.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<IReadOnlyList<HuggingFaceModel>> SearchAsync(
        string query,
        string? token,
        CancellationToken cancellationToken = default)
    {
        // filter=gguf restricts to repositories tagged as containing GGUF weights, which is
        // what makes the results loadable by LLamaSharp rather than merely relevant.
        string url = $"{ApiBase}/models?filter=gguf&sort=downloads&direction=-1&limit={SearchLimit}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"&search={Uri.EscapeDataString(query.Trim())}";
        }

        using HttpRequestMessage request = BuildRequest(HttpMethod.Get, url, token);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, "search", cancellationToken).ConfigureAwait(false);

        string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        List<SearchItem>? items = JsonSerializer.Deserialize<List<SearchItem>>(payload, _serializerOptions);

        if (items is null)
        {
            return [];
        }

        List<HuggingFaceModel> models = [];
        foreach (SearchItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            models.Add(new HuggingFaceModel(
                RepoId: item.Id,
                Downloads: item.Downloads,
                Likes: item.Likes,
                IsGated: item.Gated.HasValue && item.Gated.Value.ValueKind is not JsonValueKind.False and not JsonValueKind.Null));
        }

        return models;
    }

    /// <summary>
    /// Lists the GGUF files in a repository, smallest first.
    /// </summary>
    /// <remarks>
    /// Ordered by size because that is the axis the user is actually choosing on — a repo
    /// commonly holds the same model at six quantizations from 3 GB to 30 GB.
    /// </remarks>
    public async Task<IReadOnlyList<HuggingFaceFile>> ListGgufFilesAsync(
        string repoId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoId);

        string url = $"{ApiBase}/models/{repoId}/tree/main?recursive=true";

        using HttpRequestMessage request = BuildRequest(HttpMethod.Get, url, token);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, $"file listing for {repoId}", cancellationToken).ConfigureAwait(false);

        string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        List<TreeItem>? items = JsonSerializer.Deserialize<List<TreeItem>>(payload, _serializerOptions);

        if (items is null)
        {
            return [];
        }

        List<HuggingFaceFile> files = [];
        foreach (TreeItem item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Path) ||
                !item.Path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Weights are stored via LFS, where the real size lives on the lfs object; the
            // top-level size is the pointer file and is misleadingly tiny.
            long size = item.Lfs?.Size ?? item.Size;
            files.Add(new HuggingFaceFile(item.Path, size));
        }

        return [.. files.OrderBy(f => f.SizeBytes)];
    }

    /// <summary>
    /// Fetches descriptive detail for a repository: what the model is, how big it is, and
    /// what context it supports.
    /// </summary>
    /// <remarks>
    /// Two requests, because the useful information is split. The model endpoint carries the
    /// GGUF metadata — architecture, parameter count and context length, all read from the
    /// weights themselves rather than guessed from a filename — while the prose lives in the
    /// model card. A missing or unreadable card is not an error; the rest still displays.
    /// </remarks>
    public async Task<HuggingFaceModelDetail> GetDetailAsync(
        string repoId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoId);

        using HttpRequestMessage request = BuildRequest(HttpMethod.Get, $"{ApiBase}/models/{repoId}", token);
        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, $"details for {repoId}", cancellationToken).ConfigureAwait(false);

        string payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        DetailItem? item = JsonSerializer.Deserialize<DetailItem>(payload, _serializerOptions);

        string summary = await TryGetCardSummaryAsync(repoId, token, cancellationToken).ConfigureAwait(false);

        return new HuggingFaceModelDetail(
            RepoId: repoId,
            Summary: summary,
            Architecture: item?.Gguf?.Architecture ?? string.Empty,
            ParameterCount: item?.Gguf?.Total ?? 0,
            ContextLength: item?.Gguf?.ContextLength ?? 0,
            License: item?.CardData?.License ?? string.Empty,
            Tags: FilterTags(item?.Tags));
    }

    /// <summary>
    /// Reads the model card, returning an empty summary rather than failing.
    /// </summary>
    private async Task<string> TryGetCardSummaryAsync(
        string repoId,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(
                HttpMethod.Get,
                $"{ResolveBase}/{repoId}/resolve/main/README.md",
                token);

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            string markdown = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ModelCardSummary.Extract(markdown);
        }
        catch (HttpRequestException)
        {
            // A description is a nicety; losing it must not cost the numbers alongside it.
            return string.Empty;
        }
    }

    /// <summary>
    /// Keeps the tags that describe the model and drops the ones that describe the hosting.
    /// </summary>
    /// <remarks>
    /// The raw list mixes both freely: <c>code</c> and <c>chat</c> sit alongside
    /// <c>region:us</c>, <c>endpoints_compatible</c> and a string of <c>arxiv:</c> citations.
    /// Namespaced tags are infrastructure by convention, and the rest is a small denylist.
    /// </remarks>
    private static List<string> FilterTags(List<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        HashSet<string> noise = new(StringComparer.OrdinalIgnoreCase)
        {
            "transformers", "gguf", "safetensors", "pytorch", "endpoints_compatible",
            "autotrain_compatible", "text-generation-inference", "text-generation"
        };

        List<string> kept = [];
        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag) ||
                tag.Contains(':', StringComparison.Ordinal) ||
                noise.Contains(tag))
            {
                continue;
            }

            kept.Add(tag);
            if (kept.Count == 8)
            {
                break;
            }
        }

        return kept;
    }

    /// <summary>
    /// Lists the repository's selectable models, with split sets collapsed into one entry
    /// each, smallest first.
    /// </summary>
    public async Task<IReadOnlyList<HuggingFaceDownload>> ListDownloadsAsync(
        string repoId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HuggingFaceFile> files = await ListGgufFilesAsync(repoId, token, cancellationToken)
            .ConfigureAwait(false);

        return GroupDownloads(files);
    }

    /// <summary>
    /// Collapses a flat file listing into selectable models, joining the parts of each split
    /// set back together.
    /// </summary>
    /// <remarks>
    /// A split set whose first part is missing from the listing is dropped: llama.cpp is given
    /// the first part and reads the rest from alongside it, so without it there is nothing
    /// loadable to offer.
    /// </remarks>
    public static IReadOnlyList<HuggingFaceDownload> GroupDownloads(IReadOnlyList<HuggingFaceFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        List<HuggingFaceDownload> downloads = [];
        Dictionary<string, List<HuggingFaceFile>> sets = [];

        foreach (HuggingFaceFile file in files)
        {
            // A vision projector is not a model you can chat with, and it is the smallest
            // file in the repository — first in a size-ordered list, and useless if picked.
            if (GgufShards.IsAuxiliary(file.FileName))
            {
                continue;
            }

            if (!GgufShards.TryParse(file.FileName, out GgufShard shard))
            {
                downloads.Add(new HuggingFaceDownload([file]));
                continue;
            }

            // Directory-qualified: a repository may publish several split sets, and two of
            // them in different folders can share a base name.
            string directory = System.IO.Path.GetDirectoryName(file.Path) ?? string.Empty;
            string key = $"{directory} {shard.BaseName} {shard.Total}";

            if (!sets.TryGetValue(key, out List<HuggingFaceFile>? parts))
            {
                parts = [];
                sets[key] = parts;
            }

            parts.Add(file);
        }

        foreach (List<HuggingFaceFile> parts in sets.Values)
        {
            List<HuggingFaceFile> ordered = [.. parts.OrderBy(ShardIndex)];
            if (ShardIndex(ordered[0]) != 1)
            {
                continue;
            }

            downloads.Add(new HuggingFaceDownload(ordered));
        }

        // Smallest first, on the total rather than the part size — the number that decides
        // whether the model fits.
        return [.. downloads.OrderBy(d => d.TotalBytes)];

        static int ShardIndex(HuggingFaceFile file)
            => GgufShards.TryParse(file.FileName, out GgufShard shard) ? shard.Index : int.MaxValue;
    }

    /// <summary>
    /// Downloads every part of a model, resuming any partial transfers, and reports progress
    /// across the set as a whole.
    /// </summary>
    /// <returns>The path to pass to llama.cpp: the only file, or the first part.</returns>
    public async Task<string> DownloadModelAsync(
        string repoId,
        HuggingFaceDownload download,
        string destinationDirectory,
        string? token,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoId);
        ArgumentNullException.ThrowIfNull(download);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        long completedBytes = 0;
        long totalBytes = download.TotalBytes;
        string? primaryPath = null;

        foreach (HuggingFaceFile part in download.Parts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destination = System.IO.Path.Combine(destinationDirectory, part.FileName);
            long partBase = completedBytes;

            // Rebase each part's progress onto the set, so the bar tracks the whole download
            // rather than restarting at zero on every part.
            IProgress<DownloadProgress>? partProgress = progress is null
                ? null
                : new Progress<DownloadProgress>(p => progress.Report(
                    new DownloadProgress(partBase + p.BytesDownloaded, totalBytes, p.BytesPerSecond)));

            await DownloadAsync(repoId, part, destination, token, partProgress, cancellationToken)
                .ConfigureAwait(false);

            completedBytes += part.SizeBytes;
            primaryPath ??= destination;
        }

        return primaryPath ?? throw new InvalidOperationException("The model has no files to download.");
    }

    /// <summary>
    /// Downloads a file, resuming a previous partial transfer when one is present.
    /// </summary>
    /// <param name="repoId">Repository id.</param>
    /// <param name="file">File to fetch.</param>
    /// <param name="destinationPath">Final path. A <c>.partial</c> sibling is used while transferring.</param>
    /// <param name="token">Optional access token.</param>
    /// <param name="progress">Receives progress updates.</param>
    /// <param name="cancellationToken">Cancels the transfer, leaving the partial file for a later resume.</param>
    public async Task DownloadAsync(
        string repoId,
        HuggingFaceFile file,
        string destinationPath,
        string? token,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoId);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string partialPath = destinationPath + PartialSuffix;
        long existingBytes = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        string url = $"{ResolveBase}/{repoId}/resolve/main/{file.Path}";
        using HttpRequestMessage request = BuildRequest(HttpMethod.Get, url, token);

        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        // A server that ignores the range restarts from zero; honour that rather than
        // appending to a prefix the new bytes do not continue.
        bool resuming = response.StatusCode == HttpStatusCode.PartialContent;
        if (existingBytes > 0 && !resuming)
        {
            existingBytes = 0;
        }

        await EnsureSuccessAsync(response, $"download of {file.FileName}", cancellationToken).ConfigureAwait(false);

        long? total = response.Content.Headers.ContentLength is long length
            ? length + existingBytes
            : file.SizeBytes > 0 ? file.SizeBytes : null;

        FileMode mode = resuming ? FileMode.Append : FileMode.Create;

        await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (FileStream destination = new(partialPath, mode, FileAccess.Write, FileShare.None, bufferSize: 1 << 20, useAsync: true))
        {
            await CopyWithProgressAsync(source, destination, existingBytes, total, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        // Only now is the file loadable. Replacing at the end means an interrupted download
        // can never be mistaken for a usable model by the local catalog scan.
        File.Move(partialPath, destinationPath, overwrite: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long startingBytes,
        long? total,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1 << 20];
        long written = startingBytes;

        Stopwatch clock = Stopwatch.StartNew();
        long lastReportBytes = startingBytes;
        TimeSpan lastReport = TimeSpan.Zero;

        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;

            // Throttle reporting: a 1 MB buffer on a fast link would otherwise post
            // thousands of updates a second at the UI thread.
            TimeSpan now = clock.Elapsed;
            if (progress is not null && (now - lastReport) >= TimeSpan.FromMilliseconds(200))
            {
                double seconds = (now - lastReport).TotalSeconds;
                double rate = seconds > 0 ? (written - lastReportBytes) / seconds : 0;
                progress.Report(new DownloadProgress(written, total, rate));

                lastReport = now;
                lastReportBytes = written;
            }
        }

        progress?.Report(new DownloadProgress(written, total ?? written, 0));
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string? token)
    {
        HttpRequestMessage request = new(method, url);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // 401/403 on Hugging Face nearly always means a gated repository rather than a bad
        // token, so say what to actually do about it.
        string hint = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? " This repository may be gated — accept its terms on huggingface.co and add an access token in Options."
            : string.Empty;

        throw new HttpRequestException(
            $"Hugging Face {operation} failed ({(int)response.StatusCode} {response.ReasonPhrase}).{hint} {body}",
            null,
            response.StatusCode);
    }

    private sealed record SearchItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("downloads")]
        public long Downloads { get; init; }

        [JsonPropertyName("likes")]
        public long Likes { get; init; }

        // "gated" is polymorphic: false, or a string such as "auto"/"manual".
        [JsonPropertyName("gated")]
        public JsonElement? Gated { get; init; }
    }

    private sealed record DetailItem
    {
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; init; }

        [JsonPropertyName("cardData")]
        public CardDataItem? CardData { get; init; }

        // Present on repositories carrying GGUF weights: metadata read from the file itself.
        [JsonPropertyName("gguf")]
        public GgufItem? Gguf { get; init; }
    }

    private sealed record CardDataItem
    {
        // Polymorphic in the wild: usually a string, occasionally a list.
        [JsonPropertyName("license")]
        public JsonElement? LicenseValue { get; init; }

        public string License => LicenseValue?.ValueKind switch
        {
            JsonValueKind.String => LicenseValue.Value.GetString() ?? string.Empty,
            JsonValueKind.Array => LicenseValue.Value.GetArrayLength() > 0
                ? LicenseValue.Value[0].GetString() ?? string.Empty
                : string.Empty,
            _ => string.Empty
        };
    }

    private sealed record GgufItem
    {
        [JsonPropertyName("total")]
        public long Total { get; init; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; init; }

        [JsonPropertyName("context_length")]
        public int ContextLength { get; init; }
    }

    private sealed record TreeItem
    {
        [JsonPropertyName("path")]
        public string? Path { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("lfs")]
        public LfsInfo? Lfs { get; init; }
    }

    private sealed record LfsInfo
    {
        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
