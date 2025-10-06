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

    private CancellationTokenSource? _pumpCts;
    private Task? _discordToChatPump;
    private Task? _chatToDiscordPump;

    public async Task StartAsync()
    {
        await _gateway.ConnectAsync().ConfigureAwait(false);
        // Create a linked CTS so we can cancel pumps cooperatively during Dispose.
        _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionToken);
        CancellationToken ct = _pumpCts.Token;
        _discordToChatPump = Task.Run(async () =>
        {
            await foreach ((long _, DiscordEntry entry) in _discordJournal.TailAsync(0, ct))
            {
                ChatEntry chat = await _transmuter.TransmuteIn(entry, ct).ConfigureAwait(false);
                await _chatJournal.WriteAsync(chat, ct).ConfigureAwait(false);
            }
        }, ct);

        _chatToDiscordPump = Task.Run(async () =>
        {
            await foreach ((long _, ChatEntry entry) in _chatJournal.TailAsync(0, ct))
            {
                DiscordEntry discord = await _transmuter.TransmuteOut(entry, ct).ConfigureAwait(false);
                await _discordJournal.WriteAsync(discord, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Ensure pumps are cancelled so async enumerables can complete cooperatively.
            _pumpCts?.Cancel();

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
            _pumpCts?.Dispose();
            _pumpCts = null;
            _discordToChatPump = null;
            _chatToDiscordPump = null;
            GC.SuppressFinalize(this);
        }
    }
}
