// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Testing.Harness.Scripting;

/// <summary>
/// Base interface for scripted OpenAI responses used in testing.
/// </summary>
public interface IScriptedResponse
{
    /// <summary>
    /// Gets the model name to use for this response.
    /// </summary>
    string Model { get; }
}

/// <summary>
/// A complete (non-streaming) response from OpenAI.
/// </summary>
/// <param name="Content">The full response content.</param>
/// <param name="Model">The model name (defaults to gpt-4o).</param>
public sealed record ScriptedCompleteResponse(string Content, string Model = "gpt-4o") : IScriptedResponse;

/// <summary>
/// A streaming response from OpenAI, delivered as a sequence of chunks.
/// </summary>
/// <param name="Chunks">The response content split into chunks.</param>
/// <param name="Model">The model name (defaults to gpt-4o).</param>
public sealed record ScriptedStreamingResponse(IReadOnlyList<string> Chunks, string Model = "gpt-4o") : IScriptedResponse;

/// <summary>
/// A streaming response with thought/reasoning chunks followed by response chunks.
/// </summary>
/// <param name="ThoughtChunks">The thought/reasoning content chunks.</param>
/// <param name="ResponseChunks">The response content chunks.</param>
/// <param name="Model">The model name (defaults to gpt-4o).</param>
public sealed record ScriptedStreamingWithThoughtsResponse(
    IReadOnlyList<string> ThoughtChunks,
    IReadOnlyList<string> ResponseChunks,
    string Model = "gpt-4o") : IScriptedResponse;
