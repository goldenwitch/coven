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
    IImbuingTransmuter<DiscordEntry, long, ChatEntry> afferentTransmuter,
    IImbuingTransmuter<ChatEntry, long, DiscordEntry> efferentTransmuter,
    IShatterPolicy<ChatEntry> shatterPolicy,
    ILogger<DiscordChatSession> logger)
{
    private readonly DiscordGatewayConnection _discordGatewayConnection = discordGatewayConnection ?? throw new ArgumentNullException(nameof(discordGatewayConnection));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IImbuingTransmuter<DiscordEntry, long, ChatEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<ChatEntry, long, DiscordEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly IShatterPolicy<ChatEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly ILogger<DiscordChatSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public DiscordChatSession Create(CancellationToken sessionToken)
    {
        return new DiscordChatSession(_discordGatewayConnection, _discordJournal, _chatJournal, _afferentTransmuter, _efferentTransmuter, _shatterPolicy, _logger, sessionToken);
    }
}
