// SPDX-License-Identifier: BUSL-1.1

using Coven.Transmutation;

namespace Coven.Chat.Ui;

/// <summary>
/// Maps between UI-specific entries and generic Chat entries.
/// Supports imbuing with the source record position for position-based ACKs.
/// </summary>
internal sealed class UiChatTransmuter(UiChatClientConfig config)
    : IImbuingTransmuter<UiChatEntry, long, ChatEntry>,
      IImbuingTransmuter<ChatEntry, long, UiChatEntry>
{
    private readonly UiChatClientConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    // UI → Chat (afferent):
    // - UiChatAfferent -> ChatAfferent (position ignored)
    // - everything else -> ChatAck(position)
    public Task<ChatEntry> Transmute(UiChatEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            UiChatAfferent incoming => Task.FromResult<ChatEntry>(new ChatAfferent(incoming.Sender, incoming.Text)),
            UiChatEfferent outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, Reagent)),
            UiChatChunk chunk => Task.FromResult<ChatEntry>(new ChatAck(chunk.Sender, Reagent)),
            UiChatAck => Task.FromResult<ChatEntry>(new ChatAck(Input.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    // Chat → UI (efferent):
    // - ChatEfferent -> UiChatEfferent (rendered as a finalized message)
    // - ChatChunk    -> UiChatChunk    (rendered incrementally)
    // - everything else -> UiChatAck(position)
    public Task<UiChatEntry> Transmute(ChatEntry Input, long Reagent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            ChatEfferent outgoing => Task.FromResult<UiChatEntry>(new UiChatEfferent(_config.OutputSender, outgoing.Text)),
            ChatChunk chunk => Task.FromResult<UiChatEntry>(new UiChatChunk(_config.OutputSender, chunk.Text)),

            ChatEfferentDraft draft => Task.FromResult<UiChatEntry>(new UiChatAck(draft.Sender, Reagent)),
            ChatStreamCompleted done => Task.FromResult<UiChatEntry>(new UiChatAck(done.Sender, Reagent)),
            ChatAfferent incoming => Task.FromResult<UiChatEntry>(new UiChatAck(incoming.Sender, Reagent)),
            ChatAfferentDraft incomingDraft => Task.FromResult<UiChatEntry>(new UiChatAck(incomingDraft.Sender, Reagent)),
            ChatAck ack => Task.FromResult<UiChatEntry>(new UiChatAck(ack.Sender, Reagent)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
