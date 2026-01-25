// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;

namespace Coven.Gemini.Client;

/// <summary>A part of Gemini content; holds text or reasoning.</summary>
public sealed record GeminiPart
{
    /// <summary>Plain text content.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>Model reasoning / chain-of-thought content (when available).</summary>
    [JsonPropertyName("model_reasoning")]
    public string? ModelReasoning { get; init; }
}

/// <summary>A content block containing one or more parts with a role.</summary>
public sealed record GeminiContent
{
    /// <summary>Role: "user", "model", or "system".</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>Parts that make up this content block.</summary>
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

/// <summary>Generation configuration (temperature, top-p, etc.).</summary>
public sealed record GeminiGenerationConfig
{
    /// <summary>Temperature sampling value.</summary>
    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    /// <summary>Top-p nucleus sampling.</summary>
    [JsonPropertyName("topP")]
    public float? TopP { get; init; }

    /// <summary>Top-k sampling.</summary>
    [JsonPropertyName("topK")]
    public int? TopK { get; init; }

    /// <summary>Maximum output tokens.</summary>
    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    /// <summary>Response MIME type (e.g., application/json).</summary>
    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; init; }
}

/// <summary>Safety setting (category + threshold).</summary>
public sealed record GeminiSafetySettingDto
{
    /// <summary>Harm category.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Block threshold.</summary>
    [JsonPropertyName("threshold")]
    public string? Threshold { get; init; }
}

/// <summary>Request body for generateContent / streamGenerateContent.</summary>
public sealed record GeminiGenerateContentRequest
{
    /// <summary>Model identifier.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Conversation contents.</summary>
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; init; } = [];

    /// <summary>System instruction.</summary>
    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; init; }

    /// <summary>Generation config.</summary>
    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }

    /// <summary>Safety settings.</summary>
    [JsonPropertyName("safetySettings")]
    public List<GeminiSafetySettingDto>? SafetySettings { get; init; }
}

/// <summary>Response from generateContent / streamGenerateContent.</summary>
public sealed record GeminiGenerateContentResponse
{
    /// <summary>Error payload if present.</summary>
    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }

    /// <summary>Candidate responses.</summary>
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }

    /// <summary>Prompt feedback (safety blocking, etc.).</summary>
    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; init; }

    /// <summary>Model version used.</summary>
    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; init; }

    /// <summary>Response identifier.</summary>
    [JsonPropertyName("responseId")]
    public string? ResponseId { get; init; }
}

/// <summary>Error information from Gemini API.</summary>
public sealed record GeminiError
{
    /// <summary>HTTP-like error code.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>Error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Error status.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>A candidate response.</summary>
public sealed record GeminiCandidate
{
    /// <summary>Content of the candidate.</summary>
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    /// <summary>Finish reason.</summary>
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }

    /// <summary>Safety ratings.</summary>
    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

/// <summary>Prompt-level feedback.</summary>
public sealed record GeminiPromptFeedback
{
    /// <summary>Block reason if blocked.</summary>
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; init; }

    /// <summary>Safety ratings.</summary>
    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

/// <summary>Safety rating for a category.</summary>
public sealed record GeminiSafetyRating
{
    /// <summary>Harm category.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Harm probability.</summary>
    [JsonPropertyName("probability")]
    public string? Probability { get; init; }

    /// <summary>Whether this was blocked.</summary>
    [JsonPropertyName("blocked")]
    public bool? Blocked { get; init; }

    /// <summary>Block threshold.</summary>
    [JsonPropertyName("threshold")]
    public string? Threshold { get; init; }
}

/// <summary>Extension methods for Gemini wire types.</summary>
public static class GeminiWireTypeExtensions
{
    /// <summary>Concatenates text parts from all candidates.</summary>
    public static string GetText(this GeminiGenerateContentResponse? response)
    {
        if (response?.Candidates is null)
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new();
        foreach (GeminiCandidate candidate in response.Candidates)
        {
            if (candidate.Content?.Parts is null)
            {
                continue;
            }
            foreach (GeminiPart part in candidate.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    sb.Append(part.Text);
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>Concatenates reasoning parts from all candidates.</summary>
    public static string GetReasoning(this GeminiGenerateContentResponse? response)
    {
        if (response?.Candidates is null)
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new();
        foreach (GeminiCandidate candidate in response.Candidates)
        {
            if (candidate.Content?.Parts is null)
            {
                continue;
            }
            foreach (GeminiPart part in candidate.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.ModelReasoning))
                {
                    sb.Append(part.ModelReasoning);
                }
            }
        }
        return sb.ToString();
    }
}
