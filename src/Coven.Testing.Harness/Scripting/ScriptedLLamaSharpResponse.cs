// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Testing.Harness.Scripting;

/// <summary>
/// Marker interface for scripted LLamaSharp responses.
/// </summary>
public interface IScriptedLLamaSharpResponse
{
    /// <summary>
    /// The model name to use in the response entries.
    /// </summary>
    string Model { get; }
}

/// <summary>
/// A complete (non-streaming) scripted response.
/// </summary>
/// <param name="Content">The response content.</param>
/// <param name="Model">The model name.</param>
public sealed record ScriptedLLamaSharpCompleteResponse(
    string Content,
    string Model = "local-model") : IScriptedLLamaSharpResponse;

/// <summary>
/// A streaming scripted response delivered as chunks.
/// </summary>
/// <param name="Chunks">The response chunks.</param>
/// <param name="Model">The model name.</param>
public sealed record ScriptedLLamaSharpStreamingResponse(
    IReadOnlyList<string> Chunks,
    string Model = "local-model") : IScriptedLLamaSharpResponse;
