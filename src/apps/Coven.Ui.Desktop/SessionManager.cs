// SPDX-License-Identifier: BUSL-1.1

using Coven.Chat.Ui;
using Coven.Ui.Desktop.Logging;
using Coven.Ui.Desktop.Settings;
using Coven.Ui.Shell;

namespace Coven.Ui.Desktop;

/// <summary>How a settings change was applied.</summary>
internal enum SessionChangeKind
{
    /// <summary>Nothing relevant changed.</summary>
    None = 0,

    /// <summary>Applied in place; the conversation continues.</summary>
    HotModel = 1,

    /// <summary>The session was rebuilt; the conversation was reset.</summary>
    Rebuilt = 2,

    /// <summary>The new settings could not start a session.</summary>
    Failed = 3
}

/// <summary>Outcome of applying settings.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Error">Failure detail when <paramref name="Kind"/> is <see cref="SessionChangeKind.Failed"/>.</param>
internal sealed record SessionApplyResult(SessionChangeKind Kind, string? Error = null);

/// <summary>
/// Owns the current <see cref="CovenSession"/> and decides whether a settings change can be
/// applied in place or needs a rebuild.
/// </summary>
internal sealed class SessionManager(SettingsStore store, AppSettings settings) : IAsyncDisposable
{
    private readonly SettingsStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private CovenSession? _current;

    /// <summary>Transport shared with the user interface. Outlives any single session.</summary>
    public UiChannel Channel { get; } = new();

    /// <summary>Journal hand-off point. Outlives any single session.</summary>
    public SessionContext Context { get; } = new();

    /// <summary>Current settings.</summary>
    public AppSettings Settings { get; private set; } = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Where settings are persisted.</summary>
    public string SettingsPath => _store.FilePath;

    /// <summary>Reason the session is not running, if it is not.</summary>
    public string? StartupError { get; private set; }

    /// <summary>
    /// Whether moving from <paramref name="current"/> to <paramref name="updated"/> requires a
    /// rebuild, which resets the conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only an Anthropic model change is applied in place. The API key is baked into the
    /// gateway's <c>HttpClient</c> headers at construction, the system prompt is captured in the
    /// registered config, the provider determines which daemon is registered, and OpenAI's and
    /// Gemini's config records are immutable after registration.
    /// </para>
    /// <para>
    /// The local provider looks like it could be hot — <c>LLamaSharpClientConfig.ModelPath</c>
    /// has a setter — but the weights are loaded once by <c>LLamaWeights.LoadFromFile</c> when
    /// the gateway connects. Mutating the path afterwards would change a string and nothing
    /// else, so a local model change must rebuild.
    /// </para>
    /// </remarks>
    public static bool RequiresRebuild(AppSettings current, AppSettings updated)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(updated);

        if (updated.Provider != current.Provider)
        {
            return true;
        }

        if (!string.Equals(updated.ActiveApiKey, current.ActiveApiKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(updated.SystemPrompt, current.SystemPrompt, StringComparison.Ordinal))
        {
            return true;
        }

        bool modelChanged = !string.Equals(updated.ActiveModel, current.ActiveModel, StringComparison.Ordinal);
        return modelChanged && updated.Provider != AgentProvider.Anthropic;
    }

    /// <summary>Builds and starts the first session, if the active provider has a key.</summary>
    public void StartInitial()
    {
        if (!Settings.IsConfigured)
        {
            StartupError = Settings.ConfigurationHint;
            return;
        }

        TryStart(Settings);
    }

    /// <summary>
    /// Persists the supplied settings and brings the session in line with them.
    /// </summary>
    public async Task<SessionApplyResult> ApplyAsync(AppSettings updated)
    {
        ArgumentNullException.ThrowIfNull(updated);

        AppSettings previous = Settings;
        bool rebuildNeeded = RequiresRebuild(previous, updated);
        bool modelChanged = !string.Equals(updated.ActiveModel, previous.ActiveModel, StringComparison.Ordinal);

        try
        {
            _store.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep going: an unwritable settings file should not block the session change.
            await NoticeAsync(UiNoticeLevel.Warning, $"Settings could not be saved: {ex.Message}").ConfigureAwait(false);
        }

        Settings = updated;

        if (!rebuildNeeded)
        {
            if (!modelChanged)
            {
                return new SessionApplyResult(SessionChangeKind.None);
            }

            if (_current is not null && _current.TryApplyModel(updated.ActiveModel))
            {
                await NoticeAsync(UiNoticeLevel.Info, $"Model changed to {updated.ActiveModel}.").ConfigureAwait(false);
                return new SessionApplyResult(SessionChangeKind.HotModel);
            }
        }

        await DisposeCurrentAsync().ConfigureAwait(false);

        if (!updated.IsConfigured)
        {
            StartupError = updated.ConfigurationHint;
            return new SessionApplyResult(SessionChangeKind.Failed, StartupError);
        }

        if (!TryStart(updated))
        {
            return new SessionApplyResult(SessionChangeKind.Failed, StartupError);
        }

        await NoticeAsync(
            UiNoticeLevel.Info,
            $"Session restarted on {updated.Provider} / {updated.ActiveModel}.").ConfigureAwait(false);

        return new SessionApplyResult(SessionChangeKind.Rebuilt);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Channel.Complete();
        await DisposeCurrentAsync().ConfigureAwait(false);
    }

    private bool TryStart(AppSettings target)
    {
        try
        {
            _current = CovenSession.Create(target, Channel, Context);
            _current.Start();
            StartupError = null;
            return true;
        }
        catch (Exception ex)
        {
            // Covenant validation failures land here with actionable text; startup failures
            // arrive wrapped, so the cause has to be dug out of the chain.
            StartupError = ExceptionText.Describe(ex);
            _current = null;
            Context.Fail(ex);
            return false;
        }
    }

    private async Task DisposeCurrentAsync()
    {
        if (_current is null)
        {
            return;
        }

        CovenSession session = _current;
        _current = null;
        await session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task NoticeAsync(UiNoticeLevel level, string text)
    {
        if (_current is null)
        {
            return;
        }

        try
        {
            await _current.NoticeAsync(level, text).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException)
        {
            // The session is going away; the notice has nowhere to land.
        }
    }
}
