// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Agents.Claude;

/// <summary>
/// Converts Claude journal entries to Claude API message format.
/// </summary>
internal sealed class ClaudeEntryToMessageTransmuter : ITransmuter<ClaudeEntry, ClaudeMessage>
{
    public Task<ClaudeMessage> Transmute(ClaudeEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ClaudeEfferent efferent => Task.FromResult(new ClaudeMessage
            {
                Role = "user",
                Content = efferent.Text
            }),
            ClaudeAfferent afferent => Task.FromResult(new ClaudeMessage
            {
                Role = "assistant",
                Content = afferent.Text
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(Input), $"Cannot convert {Input.GetType().Name} to ClaudeMessage")
        };
    }
}
