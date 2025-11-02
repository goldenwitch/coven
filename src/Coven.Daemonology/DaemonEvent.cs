namespace Coven.Daemonology;

/// <summary>
/// Base record for daemon lifecycle events emitted to journals.
/// </summary>
public abstract record DaemonEvent;

internal sealed record StatusChanged(Status NewStatus) : DaemonEvent;

internal sealed record FailureOccurred(Exception Exception) : DaemonEvent;
