// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Testing.Harness.Scripting;

/// <summary>
/// Base interface for scripted Gemini responses used in testing.
/// </summary>
public interface IScriptedGeminiResponse
{
    /// <summary>
    /// Gets the model name to use for this response.
    /// </summary>
    string Model { get; }
}

/// <summary>
/// A complete (non-streaming) response from Gemini.
/// </summary>
/// <param name="Content">The full response content.</param>
/// <param name="Model">The model name (defaults to gemini-2.0-flash).</param>
public sealed record ScriptedGeminiCompleteResponse(string Content, string Model = "gemini-2.0-flash") : IScriptedGeminiResponse;

/// <summary>
/// A streaming response from Gemini, delivered as a sequence of chunks.
/// </summary>
/// <param name="Chunks">The response content split into chunks.</param>
/// <param name="Model">The model name (defaults to gemini-2.0-flash).</param>
public sealed record ScriptedGeminiStreamingResponse(IReadOnlyList<string> Chunks, string Model = "gemini-2.0-flash") : IScriptedGeminiResponse;

/// <summary>
/// A streaming response with reasoning chunks followed by response chunks.
/// </summary>
/// <param name="ReasoningChunks">The reasoning/trace content chunks.</param>
/// <param name="ResponseChunks">The response content chunks.</param>
/// <param name="Model">The model name (defaults to gemini-2.0-flash).</param>
public sealed record ScriptedGeminiStreamingWithReasoningResponse(
    IReadOnlyList<string> ReasoningChunks,
    IReadOnlyList<string> ResponseChunks,
    string Model = "gemini-2.0-flash") : IScriptedGeminiResponse;
