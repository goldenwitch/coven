// SPDX-License-Identifier: BUSL-1.1

using System.Globalization;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// How demanding a local model is to run.
/// </summary>
public enum HardwareTier
{
    /// <summary>Runs comfortably on an ordinary laptop.</summary>
    Low,

    /// <summary>Wants a mid-range discrete GPU, or plenty of system memory.</summary>
    Medium,

    /// <summary>Wants a high-end consumer GPU.</summary>
    High,

    /// <summary>Beyond a single consumer GPU.</summary>
    Workstation
}

/// <summary>
/// An estimate of what it takes to run a model, derived from its size on disk.
/// </summary>
/// <param name="WeightsBytes">Total size of the model files.</param>
/// <param name="RecommendedMemoryBytes">Weights plus working memory.</param>
/// <param name="Tier">Coarse demand classification.</param>
/// <param name="TierLabel">Short label for the tier.</param>
/// <param name="Recommendation">One-line hardware guidance.</param>
/// <param name="BitsPerWeight">
/// Average bits per parameter, when the parameter count is known. The compactness of the
/// quantization, and the most direct proxy for how much quality was traded for size.
/// </param>
public sealed record ModelHardwareProfile(
    long WeightsBytes,
    long RecommendedMemoryBytes,
    HardwareTier Tier,
    string TierLabel,
    string Recommendation,
    double? BitsPerWeight)
{
    /// <summary>Bits per weight rendered for display, or an empty string when unknown.</summary>
    public string BitsPerWeightLabel => BitsPerWeight is double bits
        ? string.Create(CultureInfo.InvariantCulture, $"{bits:0.0} bits/weight")
        : string.Empty;
}

/// <summary>
/// Estimates the memory a GGUF model needs and classifies how demanding it is.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately based on file size rather than any deeper analysis, because for a GGUF that is
/// what dominates: the weights must be resident, whether in VRAM or system RAM, for the whole
/// session. Everything else is a modest addition on top.
/// </para>
/// <para>
/// These are estimates, and the guidance says so. Actual use varies with context length — the
/// KV cache grows with it — and with how many layers are offloaded to the GPU. The number is
/// meant to answer "will this plausibly run on my machine?", not to be a budget.
/// </para>
/// </remarks>
public static class ModelHardware
{
    private const long Gib = 1024L * 1024 * 1024;

    // Working memory beyond the weights: KV cache at a moderate context, compute buffers and
    // the CUDA context. Proportional for large models, with a floor for small ones where the
    // fixed costs dominate.
    private const double OverheadFraction = 0.15;
    private static readonly long _minimumOverhead = Gib;

    /// <summary>
    /// Estimates requirements for a model of the given total size.
    /// </summary>
    /// <param name="totalBytes">Combined size of every file in the model.</param>
    /// <param name="parameterCount">Parameter count when known, for bits-per-weight.</param>
    public static ModelHardwareProfile Estimate(long totalBytes, long? parameterCount = null)
    {
        long weights = Math.Max(0, totalBytes);
        long overhead = Math.Max(_minimumOverhead, (long)(weights * OverheadFraction));
        long recommended = weights + overhead;

        (HardwareTier tier, string label, string recommendation) = Classify(recommended);

        double? bitsPerWeight = parameterCount is > 0 && weights > 0
            ? weights * 8.0 / parameterCount.Value
            : null;

        return new ModelHardwareProfile(weights, recommended, tier, label, recommendation, bitsPerWeight);
    }

    private static (HardwareTier Tier, string Label, string Recommendation) Classify(long recommendedBytes)
        => recommendedBytes switch
        {
            <= 6 * Gib => (
                HardwareTier.Low,
                "Low-spec friendly",
                "Runs on most laptops: roughly 6 GB of free memory. Any recent discrete GPU will be comfortable, and CPU-only is workable."),

            <= 14 * Gib => (
                HardwareTier.Medium,
                "Mid-spec",
                "Wants a 12–16 GB GPU for full offload. Runs on CPU with 16 GB of system RAM, but noticeably slower."),

            <= 28 * Gib => (
                HardwareTier.High,
                "High-spec",
                "Wants a 24 GB GPU such as an RTX 3090 or 4090. On CPU expect 32 GB of RAM and slow generation."),

            _ => (
                HardwareTier.Workstation,
                "Workstation",
                "Beyond a single consumer GPU. Expect multiple GPUs, or heavy offload to system RAM at low speed.")
        };
}
