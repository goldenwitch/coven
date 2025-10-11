using Coven.Core;
using Coven.Transmutation;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatSessionFactory(
    DiscordGatewayConnection discordGatewayConnection,
    IScrivener<DiscordEntry> discordJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<DiscordEntry, ChatEntry> transmuter)
{
    private readonly DiscordGatewayConnection _discordGatewayConnection = discordGatewayConnection ?? throw new ArgumentNullException(nameof(discordGatewayConnection));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<DiscordEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));

    public DiscordChatSession Create(CancellationToken sessionToken)
    {
        return new DiscordChatSession(_discordGatewayConnection, _discordJournal, _chatJournal, _transmuter, sessionToken);
    }
}
