// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.LLamaSharp;
using Coven.Ui.Desktop.HuggingFace;
using Xunit;

namespace Coven.E2E.Tests.Models;

/// <summary>
/// Tests for multi-part GGUF handling.
/// </summary>
/// <remarks>
/// Regression coverage for a real trap. The browser listed every GGUF file in a repository
/// independently, smallest first, so a model split as <c>…-00001-of-00002.gguf</c> and
/// <c>…-00002-of-00002.gguf</c> put its small tail part at the top of the list. Downloading
/// that produced a multi-gigabyte file that can never load: llama.cpp is given the first part
/// and reads the rest from alongside it.
/// </remarks>
public sealed class SplitModelTests
{
    [Theory]
    [InlineData("Qwen3.6-27B-BF16-00002-of-00002.gguf", "Qwen3.6-27B-BF16", 2, 2)]
    [InlineData("model-00001-of-00009.gguf", "model", 1, 9)]
    [InlineData("a-b-c.Q4_K_M-00003-of-00012.gguf", "a-b-c.Q4_K_M", 3, 12)]
    public void ShardNamesAreParsed(string fileName, string expectedBase, int expectedIndex, int expectedTotal)
    {
        Assert.True(GgufShards.TryParse(fileName, out GgufShard shard));

        Assert.Equal(expectedBase, shard.BaseName);
        Assert.Equal(expectedIndex, shard.Index);
        Assert.Equal(expectedTotal, shard.Total);
    }

    /// <summary>Names that only resemble the convention are not split models.</summary>
    [Theory]
    [InlineData("llama-2-7b-chat.Q4_K_M.gguf")]
    [InlineData("model-1-of-2.gguf")]           // Not zero-padded to five digits.
    [InlineData("model-00001-of-00001.gguf")]   // A single part is not a split set.
    [InlineData("model-00000-of-00002.gguf")]   // Parts are 1-based.
    [InlineData("model-00003-of-00002.gguf")]   // Index beyond the total.
    public void NonShardNamesAreRejected(string fileName)
        => Assert.False(GgufShards.TryParse(fileName, out _));

    /// <summary>
    /// The exact case that produced an unusable download: two parts become one selectable
    /// model, sized as the whole, not two competing files.
    /// </summary>
    [Fact]
    public void SplitPartsCollapseIntoASingleModel()
    {
        List<HuggingFaceFile> files =
        [
            new("Qwen3.6-27B-BF16-00002-of-00002.gguf", 4_800_000_000),
            new("Qwen3.6-27B-BF16-00001-of-00002.gguf", 49_000_000_000)
        ];

        IReadOnlyList<HuggingFaceDownload> downloads = HuggingFaceClient.GroupDownloads(files);

        HuggingFaceDownload model = Assert.Single(downloads);

        Assert.True(model.IsSplit);
        Assert.Equal(2, model.Parts.Count);
        Assert.Equal(53_800_000_000, model.TotalBytes);

        // The first part is what llama.cpp must be given, regardless of listing order.
        Assert.Equal("Qwen3.6-27B-BF16-00001-of-00002.gguf", model.Primary.FileName);
    }

    /// <summary>Single-file models and split sets coexist, ordered by true total size.</summary>
    [Fact]
    public void SingleFilesAndSplitSetsAreOrderedByTotalSize()
    {
        List<HuggingFaceFile> files =
        [
            new("big-BF16-00001-of-00002.gguf", 30_000_000_000),
            new("big-BF16-00002-of-00002.gguf", 24_000_000_000),
            new("small.Q4_K_M.gguf", 4_000_000_000)
        ];

        IReadOnlyList<HuggingFaceDownload> downloads = HuggingFaceClient.GroupDownloads(files);

        Assert.Equal(2, downloads.Count);

        // The single small file sorts first; the split set is ranked on its 54 GB total
        // rather than on either part, which is the number that decides whether it fits.
        Assert.Equal("small.Q4_K_M.gguf", downloads[0].Primary.FileName);
        Assert.False(downloads[0].IsSplit);
        Assert.True(downloads[1].IsSplit);
        Assert.Equal(54_000_000_000, downloads[1].TotalBytes);
    }

    /// <summary>
    /// A set whose first part is absent from the listing offers nothing, because there is
    /// nothing loadable to offer.
    /// </summary>
    [Fact]
    public void SplitSetMissingItsFirstPartIsNotOffered()
    {
        List<HuggingFaceFile> files =
        [
            new("model-00002-of-00003.gguf", 1000),
            new("model-00003-of-00003.gguf", 1000)
        ];

        Assert.Empty(HuggingFaceClient.GroupDownloads(files));
    }

    /// <summary>
    /// On disk, only the first part is listed as a selectable model, and it reports the size
    /// of the whole set.
    /// </summary>
    [Fact]
    public async Task LocalCatalogListsOnlyTheFirstPart()
    {
        string root = Path.Combine(Path.GetTempPath(), $"coven-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "big-00001-of-00002.gguf"), new byte[3000]);
            await File.WriteAllBytesAsync(Path.Combine(root, "big-00002-of-00002.gguf"), new byte[1000]);

            LocalModelCatalog catalog = new(root);
            IReadOnlyList<ModelDescriptor> models = await catalog.ListAsync(new ModelCatalogRequest());

            ModelDescriptor found = Assert.Single(models);

            Assert.EndsWith("big-00001-of-00002.gguf", found.Id, StringComparison.Ordinal);
            Assert.Contains("2 parts", found.DisplayName, StringComparison.Ordinal);

            // 4000 bytes across both parts, not the 3000 of the first.
            Assert.Contains("3.9 KB", found.DisplayName, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// An incomplete set is called out by name. Loading one fails deep inside llama.cpp with
    /// a message about tensors that leads nowhere near the real problem.
    /// </summary>
    [Fact]
    public async Task LocalCatalogFlagsAnIncompleteSet()
    {
        string root = Path.Combine(Path.GetTempPath(), $"coven-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "big-00001-of-00003.gguf"), new byte[1000]);

            LocalModelCatalog catalog = new(root);
            IReadOnlyList<ModelDescriptor> models = await catalog.ListAsync(new ModelCatalogRequest());

            ModelDescriptor found = Assert.Single(models);
            Assert.Contains("INCOMPLETE — 1 of 3 parts", found.DisplayName, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
