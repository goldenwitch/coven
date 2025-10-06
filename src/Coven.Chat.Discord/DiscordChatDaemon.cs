using Coven.Core;
using Coven.Daemonology;

namespace Coven.Chat.Discord;

internal sealed class DiscordChatDaemon(
    IScrivener<DaemonEvent> scrivener,
    DiscordChatSessionFactory sessionFactory) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly DiscordChatSessionFactory _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    private CancellationTokenSource? _sessionCts;
    private DiscordChatSession? _session;

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
        await Transition(Status.Completed, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
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
