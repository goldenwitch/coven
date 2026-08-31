// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;
using Coven.Transmutation;
using Microsoft.Extensions.Logging;

namespace Coven.Chat.Ui;

internal sealed class UiChatSession(
    UiChatGatewayConnection gateway,
    IScrivener<UiChatEntry> uiJournal,
    IScrivener<ChatEntry> chatJournal,
    IImbuingTransmuter<UiChatEntry, long, ChatEntry> afferentTransmuter,
    IImbuingTransmuter<ChatEntry, long, UiChatEntry> efferentTransmuter,
    ILogger<UiChatSession> logger,
    CancellationToken sessionToken) : IAsyncDisposable
{
    private readonly UiChatGatewayConnection _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IScrivener<UiChatEntry> _uiJournal = uiJournal ?? throw new ArgumentNullException(nameof(uiJournal));
    private readonly IScrivener<ChatEntry> _chatJournal = chatJournal ?? throw new ArgumentNullException(nameof(chatJournal));
    private readonly IImbuingTransmuter<UiChatEntry, long, ChatEntry> _afferentTransmuter = afferentTransmuter ?? throw new ArgumentNullException(nameof(afferentTransmuter));
    private readonly IImbuingTransmuter<ChatEntry, long, UiChatEntry> _efferentTransmuter = efferentTransmuter ?? throw new ArgumentNullException(nameof(efferentTransmuter));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationToken _sessionToken = sessionToken;

    private Task? _uiToChatPump;
    private Task? _chatToUiPump;

    // Faults as soon as any pump does, so the daemon can report it rather than leaving the
    // interface waiting on a turn that is already dead. The gateway's input pump is included:
    // it is the path the user's own messages travel, and it can fail on its own.
    internal Task Completion => _uiToChatPump is not null && _chatToUiPump is not null
        ? DaemonPumps.WhenAllOrFirstFault(_uiToChatPump, _chatToUiPump, _gateway.Completion)
        : Task.CompletedTask;

    public async Task StartAsync()
    {
        CancellationToken ct = _sessionToken;
        await _gateway.ConnectAsync(ct).ConfigureAwait(false);

        _uiToChatPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, UiChatEntry entry) in _uiJournal.TailAsync(0, ct))
                {
                    // Only user submissions travel toward the chat journal; everything
                    // else would loop straight back out to the UI.
                    if (entry is not UiChatAfferent)
                    {
                        continue;
                    }

                    ChatEntry chat = await _afferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    long chatPosition = await _chatJournal.WriteAsync(chat, ct).ConfigureAwait(false);
                    UiChatLog.InboundAppended(_logger, chat.GetType().Name, chatPosition);
                }
            }
            catch (OperationCanceledException)
            {
                UiChatLog.PumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                UiChatLog.PumpFailed(_logger, ex);
                throw;
            }
        }, ct);

        _chatToUiPump = Task.Run(async () =>
        {
            try
            {
                await foreach ((long position, ChatEntry entry) in _chatJournal.TailAsync(0, ct))
                {
                    // Finalized messages and streaming fragments are the only renderables.
                    if (entry is not (ChatEfferent or ChatChunk))
                    {
                        continue;
                    }

                    UiChatEntry ui = await _efferentTransmuter.Transmute(entry, position, ct).ConfigureAwait(false);
                    await _uiJournal.WriteAsync(ui, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                UiChatLog.PumpCanceled(_logger);
            }
            catch (Exception ex)
            {
                UiChatLog.PumpFailed(_logger, ex);
                throw;
            }
        }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_uiToChatPump is not null && _chatToUiPump is not null)
            {
                try
                {
                    await Completion.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    // Expected during cooperative shutdown.
                }
                catch (Exception)
                {
                    // Already reported by the daemon's monitor; disposal must still finish.
                }
            }
        }
        finally
        {
            await _gateway.DrainAsync().ConfigureAwait(false);
            _uiToChatPump = null;
            _chatToUiPump = null;
            GC.SuppressFinalize(this);
        }
    }
}
