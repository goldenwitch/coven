// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;

namespace Coven.Agents.Gemini;

internal sealed record GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("model_reasoning")]
    public string? ModelReasoning { get; init; }
}

internal sealed record GeminiContent
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

internal sealed record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    [JsonPropertyName("topP")]
    public float? TopP { get; init; }

    [JsonPropertyName("topK")]
    public int? TopK { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; init; }
}

internal sealed record GeminiSafetySettingDto
{
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("threshold")]
    public string? Threshold { get; init; }
}

internal sealed record GeminiGenerateContentRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; init; } = [];

    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; init; }

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }

    [JsonPropertyName("safetySettings")]
    public List<GeminiSafetySettingDto>? SafetySettings { get; init; }
}

internal sealed record GeminiGenerateContentResponse
{
    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }

    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; init; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; init; }

    [JsonPropertyName("responseId")]
    public string? ResponseId { get; init; }
}

internal sealed record GeminiError
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

internal sealed record GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; init; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

internal sealed record GeminiSafetyRating
{
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("probability")]
    public string? Probability { get; init; }

    [JsonPropertyName("blocked")]
    public bool? Blocked { get; init; }

    [JsonPropertyName("threshold")]
    public string? Threshold { get; init; }
}
