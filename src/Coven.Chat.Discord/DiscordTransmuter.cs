using Coven.Transmutation;

namespace Coven.Chat.Discord;

public class DiscordTransmuter : IBiDirectionalTransmuter<DiscordEntry, ChatEntry>
{
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
