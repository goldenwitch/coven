// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;

namespace Coven.Agents.OpenAI;

internal sealed class OpenAIAgentDaemon(
    IScrivener<DaemonEvent> scrivener,
    OpenAIAgentSessionFactory sessionFactory) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly OpenAIAgentSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private CancellationTokenSource? _sessionCts;
    private OpenAIAgentSession? _session;
    private Task? _sessionMonitor;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _session = _sessionFactory.Create(_sessionCts.Token);
        await _session.StartAsync().ConfigureAwait(false);
        _sessionMonitor = MonitorSession(_session.Completion, _sessionCts);
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
