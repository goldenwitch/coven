// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json;
using System.Text.Json.Serialization;

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
    public List<ClaudeToolDefinition>? Tools { get; set; }
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
internal sealed class ClaudeMessage
{
    /// <summary>Gets or sets the role (user or assistant).</summary>
    public required string Role { get; set; }
    /// <summary>Gets or sets the message content (text or content blocks).</summary>
    public required ClaudeMessageContent Content { get; set; }
}

/// <summary>
/// Message content: either a text string or a list of content blocks.
/// Serializes to bare string or bare array to match Claude's API contract.
/// </summary>
[JsonConverter(typeof(ClaudeMessageContentConverter))]
internal abstract record ClaudeMessageContent
{
    /// <summary>Plain text content.</summary>
    public sealed record Text(string Value) : ClaudeMessageContent;

    /// <summary>Structured content blocks (tool use, tool result, etc.).</summary>
    public sealed record Blocks(List<ClaudeContentBlock> Items) : ClaudeMessageContent;

    /// <summary>Creates text content.</summary>
    public static ClaudeMessageContent FromText(string text) => new Text(text);

    /// <summary>Creates block content.</summary>
    public static ClaudeMessageContent FromBlocks(List<ClaudeContentBlock> blocks) => new Blocks(blocks);
}

/// <summary>
/// Serializes <see cref="ClaudeMessageContent"/> as a bare JSON string or array
/// to match Claude's Messages API contract.
/// </summary>
internal sealed class ClaudeMessageContentConverter : JsonConverter<ClaudeMessageContent>
{
    public override ClaudeMessageContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType is JsonTokenType.String
            ? new ClaudeMessageContent.Text(reader.GetString()!)
            : reader.TokenType is JsonTokenType.StartArray
                ? new ClaudeMessageContent.Blocks(
                    JsonSerializer.Deserialize<List<ClaudeContentBlock>>(ref reader, options)!)
                : throw new JsonException($"Expected string or array for ClaudeMessageContent, got {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, ClaudeMessageContent value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ClaudeMessageContent.Text text:
                writer.WriteStringValue(text.Value);
                break;
            case ClaudeMessageContent.Blocks blocks:
                JsonSerializer.Serialize(writer, blocks.Items, options);
                break;
            default:
                throw new JsonException($"Unknown ClaudeMessageContent type: {value.GetType().Name}");
        }
    }
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
    // Tool use (response from Claude)
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
    // Tool result (request to Claude)
    public string? ToolUseId { get; set; }
    public string? Content { get; set; }
    public bool? IsError { get; set; }
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

/// <summary>
/// Tool definition for Claude API requests.
/// </summary>
internal sealed class ClaudeToolDefinition
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public JsonElement? InputSchema { get; set; }
}
