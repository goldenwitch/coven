// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Console;

internal sealed class ConsoleChatSessionFactory(
    ConsoleGatewayConnection gatewayConnection,
    IScrivener<ConsoleEntry> consoleJournal,
    IScrivener<ChatEntry> chatJournal,
    IImbuingTransmuter<ConsoleEntry, long, ChatEntry> afferentTransmuter,
    IImbuingTransmuter<ChatEntry, long, ConsoleEntry> efferentTransmuter,
    ILogger<ConsoleChatSession> logger)
{
    private readonly ConsoleGatewayConnection _gatewayConnection = gatewayConnection ?? throw new ArgumentNullException(nameof(gatewayConnection));
    private readonly IScrivener<ConsoleEntry> _consoleJournal = consoleJournal ?? throw new ArgumentNullException(nameof(consoleJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IImbuingTransmuter<ConsoleEntry, long, ChatEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<ChatEntry, long, ConsoleEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger<ConsoleChatSession> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ConsoleChatSession Create(CancellationToken sessionToken)
        => new(_gatewayConnection, _consoleJournal, _chatJournal, _afferentTransmuter, _efferentTransmuter, _logger, sessionToken);
}
