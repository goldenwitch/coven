// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Lists GGUF model files available on disk.
/// </summary>
/// <remarks>
/// <para>
/// The only catalog that answers from the filesystem rather than a provider API. The models
/// directory is supplied at construction because <see cref="ModelCatalogRequest"/> describes
/// remote credentials, which have no meaning here — the request argument is ignored.
/// </para>
/// <para>
/// <see cref="ModelDescriptor.Id"/> is the <b>full path</b>, because that is what
/// <c>LLamaSharpClientConfig.ModelPath</c> needs; the display name carries the filename and
/// size, which is the number that actually decides whether a model will run on your machine.
/// </para>
/// </remarks>
/// <param name="modelsDirectory">Directory to scan. Missing directories yield an empty list.</param>
public sealed class LocalModelCatalog(string modelsDirectory) : IModelCatalog
{
    private const string GgufSearchPattern = "*.gguf";

    /// <inheritdoc />
    public string ProviderName => "Local (GGUF)";

    /// <summary>The directory this catalog scans.</summary>
    public string ModelsDirectory { get; } = modelsDirectory
        ?? throw new ArgumentNullException(nameof(modelsDirectory));

    /// <inheritdoc />
    /// <remarks><paramref name="request"/> is ignored; local models need no credentials.</remarks>
    public Task<IReadOnlyList<ModelDescriptor>> ListAsync(
        ModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A missing directory is the normal first-run state, not an error — the user has
        // simply not downloaded anything yet.
        if (string.IsNullOrWhiteSpace(ModelsDirectory) || !Directory.Exists(ModelsDirectory))
        {
            return Task.FromResult<IReadOnlyList<ModelDescriptor>>([]);
        }

        List<ModelDescriptor> models = [];

        // Recursive: downloads are grouped into per-repository subdirectories.
        foreach (string path in Directory.EnumerateFiles(ModelsDirectory, GgufSearchPattern, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only the first part of a split model is loadable; the rest are read from
            // alongside it. Listing them would offer choices that cannot work.
            if (GgufShards.IsTrailingShard(path))
            {
                continue;
            }

            // Nor can a projector hold a conversation. It sits next to a vision model as a
            // companion file, and offering it as a chat model is the same kind of dead end
            // as offering a trailing shard. The Hugging Face grouping already excludes these.
            if (GgufShards.IsAuxiliary(path))
            {
                continue;
            }

            long sizeBytes;
            DateTimeOffset modified;
            try
            {
                FileInfo info = new(path);
                sizeBytes = info.Length;
                modified = info.LastWriteTimeUtc;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that vanished or is locked mid-scan is skipped rather than failing
                // the whole listing — a partially written download should not hide the rest.
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            ModelFamilyRule rule = ModelFamilies.Resolve(Path.GetFileName(path));

            string label;
            if (GgufShards.TryParse(path, out GgufShard shard))
            {
                // Report the whole set's size and whether it is actually complete. A missing
                // part fails at load time with a message about tensors, which does not lead
                // anyone to the real problem.
                (long totalBytes, int present) = MeasureShardSet(path, shard);
                string parts = present == shard.Total
                    ? $"{shard.Total} parts"
                    : $"INCOMPLETE — {present} of {shard.Total} parts";

                label = $"{shard.BaseName} ({FormatSize(totalBytes)}, {parts})";
            }
            else
            {
                label = $"{fileName} ({FormatSize(sizeBytes)})";
            }

            models.Add(new ModelDescriptor(
                Id: path,
                DisplayName: label,
                Family: rule.Family,
                Created: modified,
                ContextWindow: null,
                Capabilities: rule.Capabilities));
        }

        // Newest file first: the thing you just downloaded is the thing you want to try.
        return Task.FromResult<IReadOnlyList<ModelDescriptor>>(
            [.. models.OrderByDescending(m => m.Created ?? DateTimeOffset.MinValue)
                      .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// Totals the sizes of every part of a split set sitting next to the first part, and
    /// counts how many are present.
    /// </summary>
    private static (long TotalBytes, int PartsPresent) MeasureShardSet(string firstPartPath, GgufShard shard)
    {
        string? directory = Path.GetDirectoryName(firstPartPath);
        if (string.IsNullOrEmpty(directory))
        {
            return (0, 0);
        }

        long total = 0;
        int present = 0;

        for (int index = 1; index <= shard.Total; index++)
        {
            string part = Path.Combine(
                directory,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{shard.BaseName}-{index:00000}-of-{shard.Total:00000}.gguf"));

            try
            {
                FileInfo info = new(part);
                if (info.Exists)
                {
                    total += info.Length;
                    present++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Treated as absent; the count is a diagnostic, not a guarantee.
            }
        }

        return (total, present);
    }

    /// <summary>Formats a byte count for display next to a model name.</summary>
    public static string FormatSize(long bytes)
    {
        const long Kib = 1024;
        const long Mib = Kib * 1024;
        const long Gib = Mib * 1024;

        return bytes switch
        {
            >= Gib => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Gib:0.#} GB"),
            >= Mib => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Mib:0.#} MB"),
            >= Kib => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Kib:0.#} KB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes} B")
        };
    }
}
