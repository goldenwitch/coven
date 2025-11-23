// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal sealed class ConsoleChatSessionFactory(
    ConsoleGatewayConnection gatewayConnection,
    IScrivener<ConsoleEntry> consoleJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<ConsoleEntry, ChatEntry> transmuter,
    ILogger<ConsoleChatSession> logger)
{
    private readonly ConsoleGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<ConsoleEntry> _consoleJournal = consoleJournal ?? throw new ArgumentNullException(nameof(consoleJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<ConsoleEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly ILogger<ConsoleChatSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ConsoleChatSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _consoleJournal, _chatJournal, _transmuter, _logger, sessionToken);
}
