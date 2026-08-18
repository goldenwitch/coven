// SPDX-License-Identifier: BUSL-1.1

using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Coven.Chat.Ui;
using Coven.Core;
using Coven.Ui.Desktop.Local;
using Coven.Ui.Desktop.Logging;
using Coven.Ui.Desktop.Settings;
using Coven.Ui.Shell;

namespace Coven.Ui.Desktop.ViewModels;

/// <summary>
/// Projects the chat and shell journals onto the main window.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SessionManager _manager;
    private readonly CancellationTokenSource _cts = new();

    private readonly Lock _chunkLock = new();
    private readonly StringBuilder _pendingChunks = new();
    private bool _flushScheduled;

    private readonly Lock _tailLock = new();
    private CancellationTokenSource? _tailCts;

    private readonly Lock _watchdogLock = new();
    private CancellationTokenSource? _watchdogCts;

    private ChatMessageViewModel? _streamingMessage;
    private bool _disposed;

    /// <summary>
    /// How long to wait for the first sign of a response before saying something.
    /// Streaming is enabled, so the first chunk should arrive within seconds; a silent wait
    /// past this point means the turn has stalled somewhere the UI cannot see.
    /// </summary>
    private static readonly TimeSpan _firstResponseTimeout = TimeSpan.FromSeconds(60);

    public MainWindowViewModel(SessionManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));

        ModelName = ShortModelName(manager.Settings.ActiveModel);
        ModelDetail = ModelDetailFor(manager.Settings.ActiveModel);
        ProviderName = manager.Settings.Provider.ToString();
        InputText = string.Empty;
        StatusText = "Ready";

        manager.Channel.Outbound += OnOutbound;
        manager.Context.SubscribeToFailure(OnSessionFailed);
        manager.Context.SubscribeToJournal(OnJournalPublished);

        if (manager.StartupError is not null)
        {
            IsFaulted = true;
            StatusText = "Session not running";
            Messages.Add(new ChatMessageViewModel("system", manager.StartupError, isUser: false));
        }
    }

    /// <summary>
    /// Shows the options dialog. Assigned by the view once a window exists to own it.
    /// </summary>
    public Func<OptionsViewModel, Task<bool>>? ShowOptionsDialog { get; set; }

    /// <summary>Draft message in the input box.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; }

    /// <summary>Single-line status shown in the header.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>Model currently backing the session, as it should read in the header.</summary>
    [ObservableProperty]
    public partial string ModelName { get; set; }

    /// <summary>
    /// The unabbreviated model, shown on hover, or empty when the header already shows it in
    /// full and a tooltip would only repeat itself.
    /// </summary>
    [ObservableProperty]
    public partial string ModelDetail { get; set; } = string.Empty;

    /// <summary>
    /// Names a model the way a header should. A local model is an absolute path, whose
    /// directory is at once the longest part and the least identifying; the file name is what
    /// tells one model from another. Hosted model ids are already short and are left alone —
    /// including ones shaped like <c>publisher/model</c>, which are not paths.
    /// </summary>
    private static string ShortModelName(string model)
        => string.IsNullOrWhiteSpace(model) ? "no model"
            : Path.IsPathRooted(model) ? Path.GetFileName(model)
            : model;

    /// <summary>The full model string, but only when it says more than the short form.</summary>
    private static string ModelDetailFor(string model)
        => string.IsNullOrWhiteSpace(model) || !Path.IsPathRooted(model) ? string.Empty : model;

    /// <summary>Provider currently backing the session.</summary>
    [ObservableProperty]
    public partial string ProviderName { get; set; }

    /// <summary>Whether the reasoning pane has anything to show.</summary>
    [ObservableProperty]
    public partial bool IsReasoningVisible { get; set; }

    /// <summary>Whether the session is not running, disabling input.</summary>
    [ObservableProperty]
    public partial bool IsFaulted { get; set; }

    /// <summary>The transcript.</summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    /// <summary>Agent reasoning, newest last.</summary>
    public ObservableCollection<string> Thoughts { get; } = [];

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manager.Channel.Outbound -= OnOutbound;
        StopResponseWatchdog();

        lock (_tailLock)
        {
            _tailCts?.Cancel();
            _tailCts?.Dispose();
            _tailCts = null;
        }

        _cts.Cancel();
        _cts.Dispose();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        string text = InputText;
        if (IsFaulted || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputText = string.Empty;
        Messages.Add(new ChatMessageViewModel("you", text, isUser: true));
        StatusText = "Waiting for the agent…";
        StartResponseWatchdog();

        await _manager.Channel.SubmitAsync(text, _cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Arms a timer that reports a stalled turn. Without this a failure anywhere downstream
    /// that does not raise — an empty stream, a hung request — leaves the UI saying
    /// "waiting for the agent" forever, with nothing to act on.
    /// </summary>
    private void StartResponseWatchdog()
    {
        CancellationToken token;
        lock (_watchdogLock)
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            token = _watchdogCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_firstResponseTimeout, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Dispatcher.UIThread.Post(ReportStalledTurn);
        }, CancellationToken.None);
    }

    /// <summary>Disarms the watchdog; any sign of life counts.</summary>
    private void StopResponseWatchdog()
    {
        lock (_watchdogLock)
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
        }
    }

    private void ReportStalledTurn()
    {
        StatusText = "No response — see the log";
        Messages.Add(new ChatMessageViewModel(
            "system",
            $"The agent has not responded in {_firstResponseTimeout.TotalSeconds:0} seconds. "
            + $"The full request trace is in {AppLog.FilePath}. "
            + "Check that the selected model is available to your account, then try Options → Refresh list.",
            isUser: false));
    }

    [RelayCommand]
    private async Task OpenOptionsAsync()
    {
        if (ShowOptionsDialog is null)
        {
            return;
        }

        using OptionsViewModel options = new(_manager.Settings, _manager.SettingsPath);
        bool saved = await ShowOptionsDialog(options).ConfigureAwait(true);
        if (!saved)
        {
            return;
        }

        StatusText = "Applying settings…";
        SessionApplyResult result = await _manager.ApplyAsync(options.BuildSettings()).ConfigureAwait(true);

        ModelName = ShortModelName(_manager.Settings.ActiveModel);
        ModelDetail = ModelDetailFor(_manager.Settings.ActiveModel);
        ProviderName = _manager.Settings.Provider.ToString();

        switch (result.Kind)
        {
            case SessionChangeKind.HotModel:
                StatusText = $"Now using {ModelName}.";
                break;

            case SessionChangeKind.Rebuilt:
                ResetTranscript($"Session restarted on {ProviderName} / {ModelName}. Previous conversation cleared.");
                IsFaulted = false;
                StatusText = "Ready";
                break;

            case SessionChangeKind.Failed:
                ResetTranscript(result.Error ?? "The session could not be started.");
                IsFaulted = true;
                StatusText = "Session not running";
                break;

            case SessionChangeKind.None:
            default:
                StatusText = "Ready";
                break;
        }
    }

    private void ResetTranscript(string systemMessage)
    {
        StopResponseWatchdog();

        // A rebuild produces a new journal, so the agent has no memory of what came before.
        // Leaving the old transcript on screen would imply continuity that does not exist.
        Messages.Clear();
        Thoughts.Clear();
        IsReasoningVisible = false;
        _streamingMessage = null;

        lock (_chunkLock)
        {
            _pendingChunks.Clear();
        }

        Messages.Add(new ChatMessageViewModel("system", systemMessage, isUser: false));
    }

    // Called from the ritual scope on a background thread, and again on every rebuild.
    private void OnJournalPublished(IScrivener<UiEntry> journal)
    {
        CancellationToken token;
        lock (_tailLock)
        {
            _tailCts?.Cancel();
            _tailCts?.Dispose();
            _tailCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            token = _tailCts.Token;
        }

        _ = TailShellJournalAsync(journal, token);
    }

    /// <summary>
    /// Describes a failure, adding what llama.cpp said when a local model is involved.
    /// </summary>
    /// <remarks>
    /// A failed load surfaces as <c>Failed to load model '&lt;path&gt;'</c>, which is exactly
    /// the information the user already had. The reason lives in the native log — an
    /// architecture the runtime does not know, a tensor the file does not contain — and is
    /// the only part worth reading.
    /// </remarks>
    private static string DescribeFailure(Exception error)
    {
        string described = ExceptionText.Describe(error);
        string? native = NativeErrorCapture.Last;

        if (string.IsNullOrWhiteSpace(native) ||
            described.Contains(native, StringComparison.Ordinal))
        {
            return described;
        }

        return $"{described}{Environment.NewLine}{Environment.NewLine}llama.cpp reported: {native}";
    }

    private void OnSessionFailed(Exception error)
    {
        StopResponseWatchdog();

        Dispatcher.UIThread.Post(() =>
        {
            IsFaulted = true;
            StatusText = "Session failed";
            Messages.Add(new ChatMessageViewModel(
                "system",
                $"{DescribeFailure(error)}{Environment.NewLine}{Environment.NewLine}Full trace: {AppLog.FilePath}",
                isUser: false));
        });
    }

    // Raised on a background journal pump — never the UI thread.
    private void OnOutbound(UiOutbound outbound)
    {
        // Any traffic at all proves the turn is alive.
        StopResponseWatchdog();

        if (outbound.Kind == UiOutboundKind.Chunk)
        {
            bool schedule = false;
            lock (_chunkLock)
            {
                _pendingChunks.Append(outbound.Text);
                if (!_flushScheduled)
                {
                    _flushScheduled = true;
                    schedule = true;
                }
            }

            // Coalesce: a post per token would starve the UI thread on long responses.
            if (schedule)
            {
                Dispatcher.UIThread.Post(FlushChunks);
            }

            return;
        }

        string text = outbound.Text;
        Dispatcher.UIThread.Post(() =>
        {
            // Drain first so a finalized message never lands ahead of its own chunks.
            FlushChunks();
            FinalizeMessage(text);
        });
    }

    private void FlushChunks()
    {
        string pending;
        lock (_chunkLock)
        {
            pending = _pendingChunks.ToString();
            _pendingChunks.Clear();
            _flushScheduled = false;
        }

        if (pending.Length == 0)
        {
            return;
        }

        if (_streamingMessage is null)
        {
            _streamingMessage = new ChatMessageViewModel("assistant", pending, isUser: false, isStreaming: true);
            Messages.Add(_streamingMessage);
        }
        else
        {
            _streamingMessage.Append(pending);
        }
    }

    private void FinalizeMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text) && _streamingMessage is null)
        {
            // The turn completed but produced no content — a stream that yielded nothing the
            // parser recognised, or a response the model declined to fill in. Say so rather
            // than sitting on "waiting".
            Messages.Add(new ChatMessageViewModel(
                "system",
                $"The agent finished without returning any text. See {AppLog.FilePath} for the request trace.",
                isUser: false));
            StatusText = "Empty response";
            return;
        }

        if (_streamingMessage is not null)
        {
            // The windowed response is authoritative over the accumulated chunks.
            _streamingMessage.Text = text;
            _streamingMessage.IsStreaming = false;
            _streamingMessage = null;
        }
        else
        {
            Messages.Add(new ChatMessageViewModel("assistant", text, isUser: false));
        }

        StatusText = "Ready";
    }

    private async Task TailShellJournalAsync(IScrivener<UiEntry> journal, CancellationToken cancellationToken)
    {
        try
        {
            await foreach ((long _, UiEntry entry) in journal.TailAsync(0, cancellationToken).ConfigureAwait(false))
            {
                switch (entry)
                {
                    case UiThought thought:
                        Dispatcher.UIThread.Post(() =>
                        {
                            Thoughts.Add(thought.Text);
                            IsReasoningVisible = true;
                        });
                        break;

                    case UiNotice notice:
                        Dispatcher.UIThread.Post(() => StatusText = notice.Text);
                        break;

                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session rebuilt or application shutting down.
        }
    }
}
