using Coven.Core;
using Coven.Transmutation;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatSessionFactory(
    DiscordClientConfig configuration,
    DiscordSocketClient socketClient,
    [FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> discordJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<DiscordEntry, ChatEntry> transmuter,
    ILogger<DiscordGatewayConnection> logger)
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly DiscordSocketClient _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<DiscordEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public DiscordChatSession Create(CancellationToken sessionToken)
    {
        DiscordGatewayConnection gateway = new(_configuration, _socketClient, _discordJournal, _logger, sessionToken);
        return new DiscordChatSession(gateway, _discordJournal, _chatJournal, _transmuter, sessionToken);
    }
}
