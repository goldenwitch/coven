namespace Coven.Daemonology;

public interface IDaemon
{
    Status Status { get; }

    Task Start(CancellationToken cancellationToken = default);

    Task Shutdown(CancellationToken cancellationToken = default);
}
