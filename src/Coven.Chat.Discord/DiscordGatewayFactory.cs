using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordGatewayFactory(
    DiscordClientConfig configuration,
    DiscordSocketClient socketClient,
    [FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener,
    ILogger<DiscordGatewayConnection> logger)
{
    private readonly DiscordClientConfig _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly DiscordSocketClient _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
    private readonly IScrivener<DiscordEntry> _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public DiscordGatewayConnection Create(CancellationToken sessionToken)
        => new(_configuration, _socketClient, _scrivener, _logger, sessionToken);
}

