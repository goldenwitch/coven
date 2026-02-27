// SPDX-License-Identifier: BUSL-1.1

using LLama;

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Builds the conversation prompt for LLamaSharp inference from journal entries.
/// </summary>
public interface ILLamaSharpTranscriptBuilder
{
    /// <summary>
    /// Builds a formatted prompt string from the journal for the given outgoing request,
    /// using the model's embedded GGUF chat template for correct formatting.
    /// </summary>
    /// <param name="weights">The loaded model weights (used to resolve the native chat template).</param>
    /// <param name="outgoing">The outgoing efferent entry triggering the request.</param>
    /// <param name="historyClip">Optional maximum number of messages to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The formatted prompt string to send to the local model.</returns>
    Task<string> BuildAsync(LLamaWeights weights, LLamaSharpEfferent outgoing, int? historyClip, CancellationToken cancellationToken);
}
