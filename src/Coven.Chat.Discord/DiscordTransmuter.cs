using Coven.Transmutation;

namespace Coven.Chat.Discord;

public class DiscordTransmuter : IBiDirectionalTransmuter<DiscordEntry, ChatEntry>
{
    public Task<ChatEntry> TransmuteIn(DiscordEntry Input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            DiscordIncoming incoming => Task.FromResult<ChatEntry>(new ChatThought(incoming.Sender, incoming.Text)),
            DiscordOutgoing outgoing => Task.FromResult<ChatEntry>(new ChatResponse("bot", outgoing.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }

    public Task<DiscordEntry> TransmuteOut(ChatEntry Output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Output switch
        {
            ChatResponse response => Task.FromResult<DiscordEntry>(new DiscordOutgoing(response.Text)),
            ChatThought thought => Task.FromResult<DiscordEntry>(new DiscordOutgoing(thought.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Output))
        };
    }
}
