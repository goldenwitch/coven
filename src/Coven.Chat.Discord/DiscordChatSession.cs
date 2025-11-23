// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Scrivener;
using Coven.Core.Streaming;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatSession(
    DiscordGatewayConnection gateway,
    IScrivener<DiscordEntry> discordJournal,
    IScrivener<ChatEntry> chatJournal,
    IBiDirectionalTransmuter<DiscordEntry, ChatEntry> transmuter,
    IShatterPolicy<ChatEntry> shatterPolicy,
    ILogger<DiscordChatSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly DiscordGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<DiscordEntry> _discordJournal = discordJournal ?? throw new ArgumentNullException(nameof(discordJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IBiDirectionalTransmuter<DiscordEntry, ChatEntry> _transmuter = transmuter ?? throw new ArgumentNullException(nameof(transmuter));
    private readonly IShatterPolicy<ChatEntry> _shatterPolicy = shatterPolicy ?? throw new ArgumentNullException(nameof(shatterPolicy));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _discordToChatPump;
    private Task? _chatToDiscordPump;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync(ct).ConfigureAwait(false);
        _discordToChatPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, DiscordEntry entry) in _discordJournal.TailAsync(0, ct))
                {
                    if (entry is DiscordAck)
                    {
                        continue;
                    }

                    DiscordLog.DiscordToChatObserved(_logger, entry.GetType().Name, position);
                    ChatEntry chat = await _transmuter.TransmuteAfferent(entry, ct).ConfigureAwait(false);
                    DiscordLog.DiscordToChatTransmuted(_logger, entry.GetType().Name, chat.GetType().Name);
                    long chatPos = await _chatJournal.WriteAsync(chat, ct).ConfigureAwait(false);
                    DiscordLog.DiscordToChatAppended(_logger, chat.GetType().Name, chatPos);
                }
                DiscordLog.DiscordToChatPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                DiscordLog.DiscordToChatPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                DiscordLog.DiscordToChatPumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _chatToDiscordPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, ChatEntry entry) in _chatJournal.TailAsync(0, ct))
                {
                    DiscordLog.ChatToDiscordObserved(_logger, entry.GetType().Name, position);

                    // Session-local shattering for drafts
                    if (entry is ChatEfferentDraft draft)
                    {
                        bool produced = false;
                        IEnumerable<ChatEntry> outputs = _shatterPolicy.Shatter(draft) ?? [];
                        foreach (ChatEntry o in outputs)
                        {
                            if (o is ChatChunk chunk)
                            {
                                produced = true;
                                long cp = await _chatJournal.WriteAsync(chunk, ct).ConfigureAwait(false);
                                DiscordLog.ChatToDiscordAppended(_logger, chunk.GetType().Name, cp);
                            }
                        }

                        if (produced)
                        {
                            long donePos = await _chatJournal.WriteAsync(new ChatStreamCompleted(draft.Sender), ct).ConfigureAwait(false);
                            DiscordLog.ChatToDiscordAppended(_logger, nameof(ChatStreamCompleted), donePos);
                            continue; // do not forward the draft itself
                        }

                        // Fallback: convert draft to fixed and write for forwarding
                        long fixedPos = await _chatJournal.WriteAsync(new ChatEfferent(draft.Sender, draft.Text), ct).ConfigureAwait(false);
                        DiscordLog.ChatToDiscordAppended(_logger, nameof(ChatEfferent), fixedPos);
                        continue; // handled by subsequent iterations
                    }

                    // Forward only fixed ChatOutgoing to Discord
                    if (entry is not ChatEfferent)
                    {
                        continue;
                    }

                    DiscordEntry discord = await _transmuter.TransmuteEfferent(entry, ct).ConfigureAwait(false);
                    DiscordLog.ChatToDiscordTransmuted(_logger, entry.GetType().Name, discord.GetType().Name);
                    long discPos = await _discordJournal.WriteAsync(discord, ct).ConfigureAwait(false);
                    DiscordLog.ChatToDiscordAppended(_logger, discord.GetType().Name, discPos);
                }
                DiscordLog.ChatToDiscordPumpCompleted(_logger);
            }
            catch (OperationCanceledException)
            {
                DiscordLog.ChatToDiscordPumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                DiscordLog.ChatToDiscordPumpFailed(_logger, ex);
                throw;
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
