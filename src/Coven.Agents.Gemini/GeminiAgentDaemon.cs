// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;

namespace Coven.Agents.Gemini;

internal sealed class GeminiAgentDaemon(
    IScrivener<DaemonEvent> scrivener,
    GeminiAgentSessionFactory sessionFactory) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly GeminiAgentSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private CancellationTokenSource? _sessionCts;
    private GeminiAgentSession? _session;
    private Task? _sessionMonitor;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _session = _sessionFactory.Create(_sessionCts.Token);
        await _session.StartAsync().ConfigureAwait(false);
        _sessionMonitor = MonitorSessionAsync(_session, _sessionCts.Token);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override async Task Shutdown(CancellationToken cancellationToken)
    {
        _sessionCts?.Cancel();
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }

        if (_sessionMonitor is not null)
        {
            try
            {
                await _sessionMonitor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cooperative shutdown.
            }
            finally
            {
                _sessionMonitor = null;
            }
        }

        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports a pump fault as a daemon failure. Without this an API error is swallowed and
    /// the caller waits indefinitely on a turn that has already failed.
    /// </summary>
    private async Task MonitorSessionAsync(GeminiAgentSession session, CancellationToken cancellationToken)
    {
        try
        {
            await session.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cooperative shutdown.
        }
        catch (Exception ex)
        {
            if (_sessionCts is not null)
            {
                await _sessionCts.CancelAsync().ConfigureAwait(false);
            }

            await Fail(ex, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Status != Status.Completed)
            {
                await Shutdown(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _session = null;
            _sessionCts?.Dispose();
            GC.SuppressFinalize(this);
        }
        return;
    }
}
