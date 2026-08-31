// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;

namespace Coven.Ui.Desktop.HuggingFace;

/// <summary>A repository returned by a Hugging Face model search.</summary>
/// <param name="RepoId">Owner-qualified repository id, e.g. <c>TheBloke/Llama-2-7B-Chat-GGUF</c>.</param>
/// <param name="Downloads">Download count over the last 30 days; the usual popularity proxy.</param>
/// <param name="Likes">Like count.</param>
/// <param name="IsGated">Whether the repository requires accepting terms or a token.</param>
/// <remarks>
/// Carries no last-modified timestamp: the list endpoint omits it unless explicitly expanded,
/// so the field would have been null on every result the browser ever shows.
/// </remarks>
internal sealed record HuggingFaceModel(
    string RepoId,
    long Downloads,
    long Likes,
    bool IsGated)
{
    /// <summary>Owner portion of the repository id.</summary>
    public string Owner => RepoId.Contains('/', StringComparison.Ordinal)
        ? RepoId[..RepoId.IndexOf('/', StringComparison.Ordinal)]
        : string.Empty;

    /// <summary>Repository name without the owner prefix.</summary>
    public string Name => RepoId.Contains('/', StringComparison.Ordinal)
        ? RepoId[(RepoId.IndexOf('/', StringComparison.Ordinal) + 1)..]
        : RepoId;
}

/// <summary>A downloadable GGUF file inside a repository.</summary>
/// <param name="Path">Path within the repository, which may include directories.</param>
/// <param name="SizeBytes">File size, or 0 when the API does not report one.</param>
internal sealed record HuggingFaceFile(string Path, long SizeBytes)
{
    /// <summary>Filename without directories.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// The quantization label embedded in the filename, such as <c>Q4_K_M</c>, or an empty
    /// string when none is recognizable. This is the single most decision-relevant part of a
    /// GGUF filename: it sets both size and quality.
    /// </summary>
    public string Quantization
    {
        get
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            string[] parts = name.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

            // Scan right-to-left: the quantization tag is conventionally the last component.
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string part = parts[i];
                if (part.Length >= 2 &&
                    (part[0] is 'Q' or 'q' or 'F' or 'f' or 'I' or 'i') &&
                    char.IsAsciiDigit(part[1]))
                {
                    // Re-join any trailing variant suffix (K, K_M, K_S, 0, 1).
                    return string.Join('_', parts[i..]).ToUpperInvariant();
                }
            }

            return string.Empty;
        }
    }
}

/// <summary>
/// One selectable model in a repository: either a single GGUF file, or a complete split set
/// presented as one item.
/// </summary>
/// <remarks>
/// The browser offers these rather than raw files. A split model's parts are not alternatives
/// to one another — all of them are needed and only the first is loadable — so letting a user
/// pick one part is a trap, and the smallest-first ordering aims them straight at it.
/// </remarks>
/// <param name="Parts">Every file to download, first part first. Never empty.</param>
internal sealed record HuggingFaceDownload(IReadOnlyList<HuggingFaceFile> Parts)
{
    /// <summary>The file llama.cpp is given: the only file, or the first part.</summary>
    public HuggingFaceFile Primary => Parts[0];

    /// <summary>Combined size of every part.</summary>
    public long TotalBytes => Parts.Sum(p => p.SizeBytes);

    /// <summary>Whether this model is published across several files.</summary>
    public bool IsSplit => Parts.Count > 1;

    /// <summary>Quantization label, taken from the first part.</summary>
    public string Quantization => Primary.Quantization;

    /// <summary>Name shown in the list.</summary>
    public string DisplayName => IsSplit
        ? $"{Coven.Agents.LLamaSharp.GgufShards.GroupKey(Primary.FileName)} ({Parts.Count} parts)"
        : Primary.FileName;
}

/// <summary>
/// Descriptive detail about a repository, gathered for the browser's details pane.
/// </summary>
/// <param name="RepoId">Owner-qualified repository id.</param>
/// <param name="Summary">First prose paragraph of the model card, or empty when it has none.</param>
/// <param name="Architecture">Model architecture reported by the GGUF metadata, e.g. <c>qwen2</c>.</param>
/// <param name="ParameterCount">Parameter count from the GGUF metadata, or 0 when unreported.</param>
/// <param name="ContextLength">Maximum context in tokens, or 0 when unreported.</param>
/// <param name="License">License identifier from the model card.</param>
/// <param name="Tags">Descriptive tags, with plumbing tags filtered out.</param>
internal sealed record HuggingFaceModelDetail(
    string RepoId,
    string Summary,
    string Architecture,
    long ParameterCount,
    int ContextLength,
    string License,
    IReadOnlyList<string> Tags)
{
    /// <summary>Parameter count in billions, or an empty string when unreported.</summary>
    public string ParameterLabel => ParameterCount > 0
        ? string.Create(CultureInfo.InvariantCulture, $"{ParameterCount / 1_000_000_000d:0.#}B parameters")
        : string.Empty;

    /// <summary>Context window rendered for display, or an empty string when unreported.</summary>
    public string ContextLabel => ContextLength > 0
        ? string.Create(CultureInfo.InvariantCulture, $"{ContextLength / 1024d:0.#}K context")
        : string.Empty;

    /// <summary>Whether there is anything worth showing.</summary>
    public bool HasSummary => Summary.Length > 0;
}

/// <summary>Progress for an in-flight download.</summary>
/// <param name="BytesDownloaded">Bytes written so far, including any resumed prefix.</param>
/// <param name="TotalBytes">Expected total, or <see langword="null"/> when the server does not report one.</param>
/// <param name="BytesPerSecond">Recent throughput estimate.</param>
internal sealed record DownloadProgress(long BytesDownloaded, long? TotalBytes, double BytesPerSecond)
{
    /// <summary>Completion fraction from 0 to 1, or <see langword="null"/> when the total is unknown.</summary>
    public double? Fraction => TotalBytes is > 0 ? Math.Clamp(BytesDownloaded / (double)TotalBytes.Value, 0, 1) : null;
}
