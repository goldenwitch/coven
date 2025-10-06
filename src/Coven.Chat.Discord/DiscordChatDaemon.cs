using Coven.Core;
using Coven.Daemonology;

namespace Coven.Chat.Discord;

internal class DiscordChatDaemon(
    IScrivener<DaemonEvent> scrivener,
    DiscordGatewayFactory gatewayFactory) : ContractDaemon(scrivener), IAsyncDisposable
{
    private readonly DiscordGatewayFactory _gatewayFactory = gatewayFactory ?? throw new ArgumentNullException(nameof(gatewayFactory));
    private DiscordGatewayConnection? _gateway;
    private CancellationTokenSource? _sessionCts;

    public override async Task Start(CancellationToken cancellationToken)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _gateway = _gatewayFactory.Create(_sessionCts.Token);

        await _gateway.ConnectAsync().ConfigureAwait(false);
        await Transition(Status.Running, cancellationToken).ConfigureAwait(false);
    }

    public override Task Shutdown(CancellationToken cancellationToken)
    {
        _sessionCts?.Cancel();
        return Transition(Status.Completed, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _gateway?.Dispose();
        _sessionCts?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
