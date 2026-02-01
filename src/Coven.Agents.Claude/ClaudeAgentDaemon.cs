// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Daemonology;

namespace Coven.Agents.Claude;

internal sealed class ClaudeAgentDaemon(
    IScrivener<DaemonEvent> scrivener,
    ClaudeAgentSessionFactory sessionFactory) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly ClaudeAgentSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private CancellationTokenSource? _sessionCts;
    private ClaudeAgentSession? _session;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _session = _sessionFactory.Create(_sessionCts.Token);
        await _session.StartAsync().ConfigureAwait(false);
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
