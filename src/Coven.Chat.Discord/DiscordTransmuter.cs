// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Chat.Discord;

/// <summary>
/// Maps between Discord-specific entries and generic Chat entries.
/// Afferent: Discord → Chat; Efferent: Chat → Discord.
/// </summary>
public class DiscordTransmuter : IBiDirectionalTransmuter<DiscordEntry, ChatEntry>
{
    /// <summary>
    /// Transmutes Discord-afferent entries into chat entries.
    /// </summary>
    /// <param name="Input">The source Discord entry.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped <see cref="ChatEntry"/>.</returns>
    public Task<ChatEntry> TransmuteAfferent(DiscordEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            DiscordAfferent incoming => Task.FromResult<ChatEntry>(new ChatAfferent(incoming.Sender, incoming.Text)),
            DiscordEfferent outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    /// <summary>
    /// Transmutes chat-efferent entries into Discord entries.
    /// </summary>
    /// <param name="Output">The source chat entry.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped <see cref="DiscordEntry"/>.</returns>
    public Task<DiscordEntry> TransmuteEfferent(ChatEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            ChatEfferent outgoing => Task.FromResult<DiscordEntry>(new DiscordEfferent(outgoing.Sender, outgoing.Text)),

            // Internal/unfixed artifacts: acknowledge only to prevent loops
            ChatEfferentDraft draft => Task.FromResult<DiscordEntry>(new DiscordAck(draft.Sender, draft.Text)),
            ChatChunk chunk => Task.FromResult<DiscordEntry>(new DiscordAck(chunk.Sender, chunk.Text)),
            ChatStreamCompleted done => Task.FromResult<DiscordEntry>(new DiscordAck(done.Sender, string.Empty)),
            ChatAfferent incoming => Task.FromResult<DiscordEntry>(new DiscordAck(incoming.Sender, incoming.Text)),
            ChatAfferentDraft incomingDraft => Task.FromResult<DiscordEntry>(new DiscordAck(incomingDraft.Sender, incomingDraft.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
