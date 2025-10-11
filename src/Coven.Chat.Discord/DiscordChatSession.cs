using Coven.Core;
using Coven.Transmutation;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatSession(
    DiscordGatewayConnection gateway,
    IScrivener<DiscordEntry> discordJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<DiscordEntry, ChatEntry> transmuter,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly DiscordGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<DiscordEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _discordToChatPump;
    private Task? _chatToDiscordPump;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync(ct).ConfigureAwait(false);
        _discordToChatPump = Task.Run(async () =>
        {
            await foreach ((long _, DiscordEntry entry) in _discordJournal.TailAsync(0, ct))
            {
                // Skip Acks
                if (entry is DiscordAck)
                {
                    continue;
                }

                ChatEntry chat = await _transmuter.TransmuteIn(entry, ct).ConfigureAwait(false);
                await _chatJournal.WriteAsync(chat, ct).ConfigureAwait(false);
            }
        }, ct);

        _chatToDiscordPump = Task.Run(async () =>
        {
            await foreach ((long _, ChatEntry entry) in _chatJournal.TailAsync(0, ct))
            {
                // Skip Acks
                if (entry is ChatAck)
                {
                    continue;
                }

                DiscordEntry discord = await _transmuter.TransmuteOut(entry, ct).ConfigureAwait(false);
                await _discordJournal.WriteAsync(discord, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_discordToChatPump is not null && _chatToDiscordPump is not null)
            {
                try
                {
                    await Task.WhenAll(_discordToChatPump, _chatToDiscordPump).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during cooperative shutdown.
                }
            }
        }
        finally
        {
            _gateway.Dispose();
            _discordToChatPump = null;
            _chatToDiscordPump = null;
            GC.SuppressFinalize(this);
        }
    }
}
