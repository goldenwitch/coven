using Coven.Transmutation;

namespace Coven.Chat.Discord;

public class DiscordTransmuter : IBiDirectionalTransmuter<DiscordEntry, ChatEntry>
{
    public Task<ChatEntry> TransmuteIn(DiscordEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            DiscordIncoming incoming => Task.FromResult<ChatEntry>(new ChatIncoming(incoming.Sender, incoming.Text)),
            DiscordOutgoing outgoing => Task.FromResult<ChatEntry>(new ChatAck(outgoing.Sender, outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<DiscordEntry> TransmuteOut(ChatEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            ChatIncoming incoming => Task.FromResult<DiscordEntry>(new DiscordAck(incoming.Sender, incoming.Text)),
            // Streaming artifacts: do not send to Discord, only acknowledge
            ChatChunk chunk => Task.FromResult<DiscordEntry>(new DiscordAck(chunk.Sender, chunk.Text)),
            ChatStreamCompleted done => Task.FromResult<DiscordEntry>(new DiscordAck(done.Sender, string.Empty)),
            ChatOutgoing outgoing => Task.FromResult<DiscordEntry>(new DiscordOutgoing(outgoing.Sender, outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }

    // Flows
    // Incoming from discord.
    // Create DiscordIncoming in the DiscordScrivener
    // Read by the tail loop
    // Create ChatIncoming in the ChatScrivener
    // Read by the tail loop
    // Create DiscordAck in the DiscordScrivener (acks are not pumped between scriveners)

    // Outgoing to discord.
    // Create ChatOutgoing in the ChatScrivener
    // Read by the tail loop
    // Create DiscordOutging in the DiscordScrivener
    // Read by tail loop
    // Create ChatAck in the ChatScrivener (acks are not pumped between scriveners)
}
