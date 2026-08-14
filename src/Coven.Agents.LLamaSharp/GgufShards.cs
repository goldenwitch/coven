// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;
using System.Text.RegularExpressions;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Recognizes the multi-part GGUF naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Large models are published split across numbered files named
/// <c>&lt;base&gt;-00001-of-00003.gguf</c>. They are not independent models and not
/// alternatives to each other: llama.cpp is given the <b>first</b> part and reads the rest
/// from alongside it, so every part must be present and only the first is loadable.
/// </para>
/// <para>
/// This exists because treating the parts as separate files is actively harmful. Listed by
/// size, the small tail part looks like the cheap quantization to try first, and downloading
/// it alone produces a file that is several gigabytes, plausible, and impossible to load.
/// </para>
/// </remarks>
public static partial class GgufShards
{
    [GeneratedRegex(
        @"^(?<base>.+)-(?<index>\d{5})-of-(?<total>\d{5})\.gguf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShardPattern();

    /// <summary>
    /// Parses a shard filename.
    /// </summary>
    /// <param name="fileName">A filename, with or without directories.</param>
    /// <param name="shard">The parsed parts on success.</param>
    /// <returns><see langword="true"/> when the name is part of a split set.</returns>
    public static bool TryParse(string fileName, out GgufShard shard)
    {
        shard = default;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        Match match = ShardPattern().Match(Path.GetFileName(fileName));
        if (!match.Success)
        {
            return false;
        }

        int index = int.Parse(match.Groups["index"].ValueSpan, CultureInfo.InvariantCulture);
        int total = int.Parse(match.Groups["total"].ValueSpan, CultureInfo.InvariantCulture);

        // A single-part set is not a split model, and a zero index is not the convention.
        if (index < 1 || total < 2 || index > total)
        {
            return false;
        }

        shard = new GgufShard(match.Groups["base"].Value, index, total);
        return true;
    }

    /// <summary>
    /// Whether the file is a part of a split set that is <b>not</b> the first, and therefore
    /// can never be loaded directly.
    /// </summary>
    public static bool IsTrailingShard(string fileName)
        => TryParse(fileName, out GgufShard shard) && shard.Index > 1;

    /// <summary>
    /// Whether a GGUF file is a companion to a model rather than a model itself.
    /// </summary>
    /// <remarks>
    /// Multimodal repositories ship a vision projector — <c>mmproj-F16.gguf</c> and similar —
    /// next to the language weights. It is a real GGUF, and at around a gigabyte it is the
    /// smallest file in the repository, so a size-ordered list puts it first. It cannot hold a
    /// conversation on its own.
    /// </remarks>
    public static bool IsAuxiliary(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string name = Path.GetFileName(fileName);
        return name.StartsWith("mmproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a path can be handed to llama.cpp as-is.
    /// </summary>
    /// <param name="modelPath">Full path to a GGUF file that exists.</param>
    /// <param name="problem">Why it cannot be loaded, when it cannot.</param>
    /// <returns><see langword="true"/> when there is a problem worth reporting.</returns>
    /// <remarks>
    /// Both failures here otherwise surface from deep inside llama.cpp as errors about
    /// missing tensors, which gives no hint that the real answer is "you have the wrong part"
    /// or "you are missing a file".
    /// </remarks>
    public static bool TryFindProblem(string modelPath, out string problem)
    {
        problem = string.Empty;

        if (string.IsNullOrWhiteSpace(modelPath) || !TryParse(modelPath, out GgufShard shard))
        {
            return false;
        }

        if (!shard.IsFirst)
        {
            problem =
                $"{Path.GetFileName(modelPath)} is part {shard.Index} of a {shard.Total}-part model. "
                + $"Only the first part can be loaded; select {shard.BaseName}-00001-of-{shard.Total:00000}.gguf "
                + "and make sure every part is downloaded.";
            return true;
        }

        string directory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        List<int> missing = [];

        for (int index = 1; index <= shard.Total; index++)
        {
            string part = Path.Combine(
                directory,
                string.Create(CultureInfo.InvariantCulture, $"{shard.BaseName}-{index:00000}-of-{shard.Total:00000}.gguf"));

            if (!File.Exists(part))
            {
                missing.Add(index);
            }
        }

        if (missing.Count > 0)
        {
            problem =
                $"{shard.BaseName} is a {shard.Total}-part model and "
                + $"part{(missing.Count == 1 ? string.Empty : "s")} "
                + $"{string.Join(", ", missing)} {(missing.Count == 1 ? "is" : "are")} missing. "
                + "Every part must be downloaded into the same folder.";
            return true;
        }

        return false;
    }

    /// <summary>
    /// The name shared by every part of a split set, or the filename itself when it is not
    /// split. Used to group parts back together.
    /// </summary>
    public static string GroupKey(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return TryParse(fileName, out GgufShard shard)
            ? shard.BaseName
            : Path.GetFileNameWithoutExtension(Path.GetFileName(fileName));
    }
}

/// <summary>One part of a split GGUF model.</summary>
/// <param name="BaseName">Name shared by every part, without the part numbering or extension.</param>
/// <param name="Index">1-based position in the set.</param>
/// <param name="Total">How many parts the set has.</param>
public readonly record struct GgufShard(string BaseName, int Index, int Total)
{
    /// <summary>Whether this is the part that llama.cpp should be given.</summary>
    public bool IsFirst => Index == 1;
}
