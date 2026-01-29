// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Claude;

/// <summary>
/// Claude Messages API request payload.
/// </summary>
internal sealed class ClaudeMessagesRequest
{
    public required string Model { get; set; }
    public required List<ClaudeMessage> Messages { get; set; }
    public required int MaxTokens { get; set; }
    public string? System { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? TopK { get; set; }
    public IReadOnlyList<string>? StopSequences { get; set; }
    public bool? Stream { get; set; }
    public ClaudeThinkingConfig? Thinking { get; set; }
}

/// <summary>
/// Configuration for extended thinking in requests.
/// </summary>
internal sealed class ClaudeThinkingConfig
{
    public string Type { get; set; } = "enabled";
    public int BudgetTokens { get; set; }
}

/// <summary>
/// A message in the Claude conversation.
/// </summary>
public sealed class ClaudeMessage
{
    /// <summary>Gets or sets the role (user or assistant).</summary>
    public required string Role { get; set; }
    /// <summary>Gets or sets the message content.</summary>
    public required string Content { get; set; }
}

/// <summary>
/// Claude Messages API response payload.
/// </summary>
internal sealed class ClaudeMessagesResponse
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Role { get; set; }
    public List<ClaudeContentBlock>? Content { get; set; }
    public string? Model { get; set; }
    public string? StopReason { get; set; }
    public ClaudeUsage? Usage { get; set; }
}

/// <summary>
/// A content block in the response (text or thinking).
/// </summary>
internal sealed class ClaudeContentBlock
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public string? Thinking { get; set; }
}

/// <summary>
/// Token usage information.
/// </summary>
internal sealed class ClaudeUsage
{
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

/// <summary>
/// SSE stream event from Claude.
/// </summary>
internal sealed class ClaudeStreamEvent
{
    public string? Type { get; set; }
    public ClaudeStreamMessage? Message { get; set; }
    public int? Index { get; set; }
    public ClaudeContentBlock? ContentBlock { get; set; }
    public ClaudeStreamDelta? Delta { get; set; }
    public ClaudeStreamError? Error { get; set; }
}

/// <summary>
/// Message info in stream events.
/// </summary>
internal sealed class ClaudeStreamMessage
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Role { get; set; }
    public string? Model { get; set; }
}

/// <summary>
/// Delta content in stream events.
/// </summary>
internal sealed class ClaudeStreamDelta
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public string? Thinking { get; set; }
    public string? StopReason { get; set; }
}

/// <summary>
/// Error in stream events.
/// </summary>
internal sealed class ClaudeStreamError
{
    public string? Type { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Options built from configuration for API requests.
/// </summary>
internal sealed class ClaudeRequestOptions
{
    public int? MaxTokens { get; set; }
    public string? System { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? TopK { get; set; }
    public IReadOnlyList<string>? StopSequences { get; set; }
    public ClaudeThinkingConfig? Thinking { get; set; }
}
