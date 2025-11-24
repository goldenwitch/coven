// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatSessionFactory(
    DiscordGatewayConnection discordGatewayConnection,
    IScrivener<DiscordEntry> discordJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<DiscordEntry, ChatEntry> transmuter,
    IShatterPolicy<ChatEntry> shatterPolicy,
    ILogger<DiscordChatSession> logger)
{
    private readonly DiscordGatewayConnection _discordGatewayConnection = discordGatewayConnection ?? throw new ArgumentNullException(nameof(discordGatewayConnection));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<DiscordEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly IShatterPolicy<ChatEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly ILogger<DiscordChatSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public DiscordChatSession Create(CancellationToken sessionToken)
    {
        return new DiscordChatSession(_discordGatewayConnection, _discordJournal, _chatJournal, _transmuter, _shatterPolicy, _logger, sessionToken);
    }
}
