// SPDX-License-Identifier: BUSL-1.1

using Coven.Agents;
using Coven.Agents.LLamaSharp;
using Coven.Ui.Desktop.HuggingFace;
using Xunit;

namespace Coven.E2E.Tests.Models;

/// <summary>
/// Tests for local GGUF discovery and Hugging Face filename parsing.
/// </summary>
public sealed class LocalModelTests
{
    /// <summary>A missing directory is first-run state, not an error.</summary>
    [Fact]
    public async Task MissingDirectoryYieldsNoModels()
    {
        LocalModelCatalog catalog = new(Path.Combine(Path.GetTempPath(), $"coven-missing-{Guid.NewGuid():N}"));

        IReadOnlyList<ModelDescriptor> models = await catalog.ListAsync(new ModelCatalogRequest());

        Assert.Empty(models);
    }

    /// <summary>
    /// GGUF files are found recursively, identified by full path, and non-GGUF files ignored.
    /// The recursion matters because downloads are grouped into per-repository folders.
    /// </summary>
    [Fact]
    public async Task FindsGgufFilesRecursivelyAndIgnoresOthers()
    {
        string root = Path.Combine(Path.GetTempPath(), $"coven-models-{Guid.NewGuid():N}");
        string nested = Path.Combine(root, "TheBloke", "Some-Model-GGUF");
        Directory.CreateDirectory(nested);

        try
        {
            string model = Path.Combine(nested, "some-model.Q4_K_M.gguf");
            await File.WriteAllTextAsync(model, "not a real model");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "ignore me");
            // A download in flight must not be offered as a loadable model.
            await File.WriteAllTextAsync(Path.Combine(nested, "other.gguf.partial"), "incomplete");

            LocalModelCatalog catalog = new(root);
            IReadOnlyList<ModelDescriptor> models = await catalog.ListAsync(new ModelCatalogRequest());

            ModelDescriptor found = Assert.Single(models);
            Assert.Equal(model, found.Id);
            Assert.Contains("some-model.Q4_K_M", found.DisplayName, StringComparison.Ordinal);
            Assert.Equal("local", found.Family);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Local models stream but expose no tools — the leaf has no tool support.</summary>
    [Fact]
    public void LocalFamilyAdvertisesStreamingOnly()
    {
        ModelFamilyRule rule = ModelFamilies.Resolve("llama-3-8b-instruct.Q4_K_M.gguf");

        Assert.Equal("local", rule.Family);
        Assert.True(rule.Capabilities.HasFlag(ModelCapabilities.Streaming));
        Assert.False(rule.Capabilities.HasFlag(ModelCapabilities.Tools));
    }

    /// <summary>
    /// Quantization is pulled out of the filename because it is the axis the user actually
    /// chooses on — the same model ships at half a dozen sizes and qualities.
    /// </summary>
    [Theory]
    [InlineData("llama-2-7b-chat.Q4_K_M.gguf", "Q4_K_M")]
    [InlineData("mistral-7b-instruct-v0.2.Q8_0.gguf", "Q8_0")]
    [InlineData("qwen2.5-coder-7b-instruct-q5_k_s.gguf", "Q5_K_S")]
    [InlineData("some-model.F16.gguf", "F16")]
    [InlineData("model-without-a-tag.gguf", "")]
    public void QuantizationIsParsedFromFileName(string fileName, string expected)
    {
        HuggingFaceFile file = new(fileName, 0);

        Assert.Equal(expected, file.Quantization);
    }

    /// <summary>The repository id splits into owner and name for display.</summary>
    [Fact]
    public void RepositoryIdSplitsIntoOwnerAndName()
    {
        HuggingFaceModel model = new("TheBloke/Llama-2-7B-Chat-GGUF", 1000, 10, IsGated: false);

        Assert.Equal("TheBloke", model.Owner);
        Assert.Equal("Llama-2-7B-Chat-GGUF", model.Name);
    }

    /// <summary>Progress reports a fraction only when the total is known.</summary>
    [Fact]
    public void DownloadProgressFractionRequiresATotal()
    {
        Assert.Equal(0.5, new DownloadProgress(50, 100, 0).Fraction);
        Assert.Null(new DownloadProgress(50, null, 0).Fraction);
    }
}
