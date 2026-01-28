// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Agents.Claude;

/// <summary>
/// Builds the conversation transcript for Claude API requests.
/// </summary>
public interface IClaudeTranscriptBuilder
{
    /// <summary>
    /// Builds a list of Claude messages from the journal for the given outgoing request.
    /// </summary>
    /// <param name="outgoing">The outgoing efferent entry triggering the request.</param>
    /// <param name="historyClip">Optional maximum number of messages to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of messages to send to Claude.</returns>
    Task<List<ClaudeMessage>> BuildAsync(ClaudeEfferent outgoing, int? historyClip, CancellationToken cancellationToken);
}
