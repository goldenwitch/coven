// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Testing.Harness.Scripting;

/// <summary>
/// Base interface for scripted Claude responses used in testing.
/// </summary>
public interface IScriptedClaudeResponse
{
    /// <summary>
    /// Gets the model name to use for this response.
    /// </summary>
    string Model { get; }
}

/// <summary>
/// A complete (non-streaming) response from Claude.
/// </summary>
/// <param name="Content">The full response content.</param>
/// <param name="Model">The model name (defaults to claude-sonnet-4-20250514).</param>
public sealed record ScriptedClaudeCompleteResponse(string Content, string Model = "claude-sonnet-4-20250514") : IScriptedClaudeResponse;

/// <summary>
/// A streaming response from Claude, delivered as a sequence of chunks.
/// </summary>
/// <param name="Chunks">The response content split into chunks.</param>
/// <param name="Model">The model name (defaults to claude-sonnet-4-20250514).</param>
public sealed record ScriptedClaudeStreamingResponse(IReadOnlyList<string> Chunks, string Model = "claude-sonnet-4-20250514") : IScriptedClaudeResponse;

/// <summary>
/// A streaming response with thinking chunks followed by response chunks.
/// </summary>
/// <param name="ThinkingChunks">The thinking/reasoning content chunks.</param>
/// <param name="ResponseChunks">The response content chunks.</param>
/// <param name="Model">The model name (defaults to claude-sonnet-4-20250514).</param>
public sealed record ScriptedClaudeStreamingWithThinkingResponse(
    IReadOnlyList<string> ThinkingChunks,
    IReadOnlyList<string> ResponseChunks,
    string Model = "claude-sonnet-4-20250514") : IScriptedClaudeResponse;
