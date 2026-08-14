// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents.LLamaSharp;
using Coven.Ui.Desktop.HuggingFace;
using Xunit;

namespace Coven.E2E.Tests.Models;

/// <summary>
/// Tests for the descriptive information shown alongside a downloadable model.
/// </summary>
public sealed class ModelDetailTests
{
    /// <summary>
    /// Frontmatter, the title and badge rows are skipped in favour of the first real
    /// sentences.
    /// </summary>
    [Fact]
    public void SummarySkipsFrontMatterAndFurniture()
    {
        const string Card = """
            ---
            license: apache-2.0
            tags:
            - chat
            ---

            # Some-Model-GGUF

            ![banner](https://example.invalid/banner.png)

            Some-Model is a compact instruction-tuned model intended for local use.
            """;

        Assert.Equal(
            "Some-Model is a compact instruction-tuned model intended for local use.",
            ModelCardSummary.Extract(Card));
    }

    /// <summary>
    /// Fenced code is skipped wholesale. Several cards open with build instructions, and
    /// treating their contents as prose produced summaries made of apt-get commands.
    /// </summary>
    [Fact]
    public void SummaryIgnoresFencedCode()
    {
        const string Card = """
            # Model

            ```bash
            apt-get update
            apt-get install build-essential cmake -y
            ```

            This model is a long-context assistant trained for retrieval-heavy workloads and general conversation.
            """;

        string summary = ModelCardSummary.Extract(Card);

        Assert.DoesNotContain("apt-get", summary, StringComparison.Ordinal);
        Assert.StartsWith("This model is a long-context assistant", summary, StringComparison.Ordinal);
    }

    /// <summary>Markdown links keep their text, and raw HTML is dropped entirely.</summary>
    [Fact]
    public void SummaryReducesLinksAndHtmlToPlainText()
    {
        const string Card = """
            # Model

            This repo contains files for [Meta Llama 2](https://example.invalid/llama), built with <a href="https://example.invalid">llama.cpp</a> and **tuned** for chat.
            """;

        Assert.Equal(
            "This repo contains files for Meta Llama 2, built with llama.cpp and tuned for chat.",
            ModelCardSummary.Extract(Card));
    }

    /// <summary>
    /// A description heading wins over earlier prose, which on real cards is usually a build
    /// or quantization aside rather than anything about the model.
    /// </summary>
    [Fact]
    public void SummaryPrefersTheDescriptionSection()
    {
        const string Card = """
            # Model

            Using llama.cpp release b3772 for quantization.

            ## Description

            A 7B instruction-tuned model for general assistance and code.
            """;

        Assert.Equal(
            "A 7B instruction-tuned model for general assistance and code.",
            ModelCardSummary.Extract(Card));
    }

    /// <summary>
    /// A section that is entirely bullets yields nothing, and the search must not spill into
    /// the next section looking for prose.
    /// </summary>
    [Fact]
    public void SummaryDoesNotSpillOutOfAnEmptySection()
    {
        const string Card = """
            # Model

            Set the build flag for CPU.

            ## Model Overview

            - Type: Causal Language Model
            - Parameters: 27B

            ## Quickstart

            For streamlined integration, we recommend using the hosted API instead of running locally.
            """;

        string summary = ModelCardSummary.Extract(Card);

        // The Quickstart prose belongs to a different section and must not be presented as
        // the model's description.
        Assert.DoesNotContain("streamlined integration", summary, StringComparison.Ordinal);
    }

    /// <summary>A card with no prose at all yields nothing rather than markup.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("---\nlicense: mit\n---\n")]
    [InlineData("# Title\n\n| a | b |\n|---|---|\n")]
    public void SummaryIsEmptyWhenThereIsNoProse(string card)
        => Assert.Equal(string.Empty, ModelCardSummary.Extract(card));

    /// <summary>Underscores survive, because they are quantization names far more often than emphasis.</summary>
    [Fact]
    public void SummaryKeepsQuantizationNamesIntact()
    {
        const string Card = """
            # Model

            Some of these quants (Q3_K_XL, Q4_K_L) quantize the embeddings to Q8_0 rather than the usual default.
            """;

        Assert.Contains("Q3_K_XL", ModelCardSummary.Extract(Card), StringComparison.Ordinal);
    }

    /// <summary>
    /// Tiers follow total size, since the weights must be resident for the whole session.
    /// </summary>
    [Theory]
    [InlineData(3L, HardwareTier.Low)]           // A 7B at Q3.
    [InlineData(8L, HardwareTier.Medium)]        // A 14B at Q4.
    [InlineData(17L, HardwareTier.High)]         // A 27B at Q4.
    [InlineData(54L, HardwareTier.Workstation)]  // A 27B at BF16.
    public void HardwareTiersFollowSize(long gigabytes, HardwareTier expected)
    {
        ModelHardwareProfile profile = ModelHardware.Estimate(gigabytes * 1024 * 1024 * 1024);

        Assert.Equal(expected, profile.Tier);
        Assert.NotEmpty(profile.Recommendation);
    }

    /// <summary>The estimate always exceeds the weights, which alone are not enough to run.</summary>
    [Fact]
    public void EstimateAddsWorkingMemoryOnTopOfTheWeights()
    {
        ModelHardwareProfile profile = ModelHardware.Estimate(4L * 1024 * 1024 * 1024);

        Assert.True(profile.RecommendedMemoryBytes > profile.WeightsBytes);
    }

    /// <summary>
    /// Bits per weight is the clearest signal of how much quality a quantization traded away,
    /// and needs the parameter count to compute.
    /// </summary>
    [Fact]
    public void BitsPerWeightIsReportedOnlyWithAParameterCount()
    {
        // A 7.6B model in a 4.7 GB file is a little under 5 bits per weight.
        ModelHardwareProfile known = ModelHardware.Estimate(4_700_000_000, 7_615_616_512);

        Assert.NotNull(known.BitsPerWeight);
        Assert.InRange(known.BitsPerWeight!.Value, 4.5, 5.2);
        Assert.Contains("bits/weight", known.BitsPerWeightLabel, StringComparison.Ordinal);

        Assert.Null(ModelHardware.Estimate(4_700_000_000).BitsPerWeight);
        Assert.Empty(ModelHardware.Estimate(4_700_000_000).BitsPerWeightLabel);
    }

    /// <summary>
    /// A vision projector is not something you can chat with, and being the smallest file in
    /// the repository it would otherwise head a size-ordered list.
    /// </summary>
    [Fact]
    public void VisionProjectorsAreNotOfferedAsModels()
    {
        List<HuggingFaceFile> files =
        [
            new("mmproj-F16.gguf", 930_000_000),
            new("mmproj-BF16.gguf", 930_000_000),
            new("Qwen3.6-27B-UD-Q4_K_XL.gguf", 17_900_000_000)
        ];

        HuggingFaceDownload model = Assert.Single(HuggingFaceClient.GroupDownloads(files));

        Assert.Equal("Qwen3.6-27B-UD-Q4_K_XL.gguf", model.Primary.FileName);
    }

    [Theory]
    [InlineData("mmproj-F16.gguf", true)]
    [InlineData("mmproj-model-f32.gguf", true)]
    [InlineData("llama-2-7b-chat.Q4_K_M.gguf", false)]
    public void AuxiliaryFilesAreRecognized(string fileName, bool expected)
        => Assert.Equal(expected, GgufShards.IsAuxiliary(fileName));
}
