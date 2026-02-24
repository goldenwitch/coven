// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.LLamaSharp;

/// <summary>
/// Builds the conversation prompt for LLamaSharp inference from journal entries.
/// </summary>
public interface ILLamaSharpTranscriptBuilder
{
    /// <summary>
    /// Builds a formatted prompt string from the journal for the given outgoing request.
    /// </summary>
    /// <param name="outgoing">The outgoing efferent entry triggering the request.</param>
    /// <param name="historyClip">Optional maximum number of messages to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The formatted prompt string to send to the local model.</returns>
    Task<string> BuildAsync(LLamaSharpEfferent outgoing, int? historyClip, CancellationToken cancellationToken);
}
