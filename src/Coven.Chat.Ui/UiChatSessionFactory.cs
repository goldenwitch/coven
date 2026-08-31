// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Ui;

internal sealed class UiChatSessionFactory(
    UiChatGatewayConnection gatewayConnection,
    IScrivener<UiChatEntry> uiJournal,
    IScrivener<ChatEntry> chatJournal,
    IImbuingTransmuter<UiChatEntry, long, ChatEntry> afferentTransmuter,
    IImbuingTransmuter<ChatEntry, long, UiChatEntry> efferentTransmuter,
    ILogger<UiChatSession> logger)
{
    private readonly UiChatGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<UiChatEntry> _uiJournal = uiJournal ?? throw new ArgumentNullException(nameof(uiJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IImbuingTransmuter<UiChatEntry, long, ChatEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<ChatEntry, long, UiChatEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger<UiChatSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public UiChatSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _uiJournal, _chatJournal, _afferentTransmuter, _efferentTransmuter, _logger, sessionToken);
}
