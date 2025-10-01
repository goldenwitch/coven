using Coven.Transmutation;

namespace Coven.Chat.Discord;

public class DiscordTransmuter : IBiDirectionalTransmuter<DiscordEntry, ChatEntry>
{
    public Task<ChatEntry> TransmuteIn(DiscordEntry Input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<DiscordEntry> TransmuteOut(ChatEntry Output, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
