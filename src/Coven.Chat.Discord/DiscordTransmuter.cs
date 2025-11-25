// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Chat.Discord;

/// <summary>
/// Maps between Discord-specific entries and generic Chat entries using position-imbued ACKs.
/// Afferent: Discord → Chat; Efferent: Chat → Discord.
/// </summary>
public class DiscordTransmuter : IImbuingTransmuter<DiscordEntry, long, ChatEntry>, IImbuingTransmuter<ChatEntry, long, DiscordEntry>
{
    /// <summary>
    /// Transmutes Discord-afferent entries into chat entries.
    /// </summary>
    /// <param name="Input">The source Discord entry.</param>
    /// <param name="Reagent">The source journal position used for position-based acknowledgements.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped <see cref="ChatEntry"/>.</returns>
    // Discord → Chat (afferent)
    public Task<ChatEntry> Transmute(DiscordEntry Input, long Reagent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            DiscordAfferent incoming => Task.FromResult<ChatEntry>(new ChatAfferent(incoming.Sender, incoming.Text)),
            DiscordEfferent outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, Reagent)),
            DiscordAck => Task.FromResult<ChatEntry>(new ChatAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    /// <summary>
    /// Transmutes chat-efferent entries into Discord entries.
    /// </summary>
    /// <param name="Input">The source chat entry.</param>
    /// <param name="Reagent">The source journal position used for position-based acknowledgements.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped <see cref="DiscordEntry"/>.</returns>
    // Chat → Discord (efferent)
    public Task<DiscordEntry> Transmute(ChatEntry Input, long Reagent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ChatEfferent outgoing => Task.FromResult<DiscordEntry>(new DiscordEfferent(outgoing.Sender, outgoing.Text)),

            // Internal/unfixed artifacts: acknowledge only to prevent loops
            ChatEfferentDraft draft => Task.FromResult<DiscordEntry>(new DiscordAck(draft.Sender, Reagent)),
            ChatChunk chunk => Task.FromResult<DiscordEntry>(new DiscordAck(chunk.Sender, Reagent)),
            ChatStreamCompleted done => Task.FromResult<DiscordEntry>(new DiscordAck(done.Sender, Reagent)),
            ChatAfferent incoming => Task.FromResult<DiscordEntry>(new DiscordAck(incoming.Sender, Reagent)),
            ChatAfferentDraft incomingDraft => Task.FromResult<DiscordEntry>(new DiscordAck(incomingDraft.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
