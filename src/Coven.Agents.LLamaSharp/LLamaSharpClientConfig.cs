// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Configuration for the LLamaSharp local LLM client.
/// </summary>
public sealed class LLamaSharpClientConfig
{
    /// <summary>Gets or sets the path to the GGUF model file (required).</summary>
    public required string ModelPath { get; set; }

    /// <summary>Gets or sets a friendly model display name. Defaults to the model filename.</summary>
    public string? ModelName { get; set; }

    /// <summary>Gets or sets the number of layers to offload to GPU. 0 = CPU only, -1 = all layers.</summary>
    public int GpuLayerCount { get; set; }

    /// <summary>Gets or sets the context window size in tokens. Default 2048.</summary>
    public uint ContextSize { get; set; } = 2048;

    /// <summary>Gets or sets the sampling temperature (0.0 to 2.0).</summary>
    public float? Temperature { get; set; }

    /// <summary>Gets or sets the top-p (nucleus) sampling parameter.</summary>
    public float? TopP { get; set; }

    /// <summary>Gets or sets the maximum number of tokens to generate.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets the system prompt to prepend to conversations.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Gets or sets the maximum number of transcript items to include in requests (default unlimited).</summary>
    public int? HistoryClip { get; set; }

    /// <summary>Gets or sets the number of CPU threads for inference. Null = auto.</summary>
    public int? Threads { get; set; }

    /// <summary>
    /// Gets or sets a marker string that separates thinking/analysis output from the actual response.
    /// When set, only the text after the last occurrence of this marker is returned.
    /// Useful for thinking/reasoning models (e.g., gpt-oss) that output an analysis channel
    /// before the final response.
    /// </summary>
    public string? ResponseStartMarker { get; set; }

    /// <summary>Gets the resolved model display name, falling back to the filename from <see cref="ModelPath"/>.</summary>
    internal string ResolvedModelName => ModelName ?? Path.GetFileNameWithoutExtension(ModelPath);
}
