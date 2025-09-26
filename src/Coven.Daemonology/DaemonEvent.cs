namespace Coven.Daemonology;

public abstract record DaemonEvent;

internal sealed record StatusChanged(Status NewStatus) : DaemonEvent;

internal sealed record FailureOccurred(Exception Exception) : DaemonEvent;

