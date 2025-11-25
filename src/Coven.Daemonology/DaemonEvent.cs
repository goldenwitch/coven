// SPDX-License-Identifier: BUSL-1.1

using System.Text.Json.Serialization;

namespace Coven.Daemonology;

/// <summary>
/// Base record for daemon lifecycle events emitted to journals.
/// </summary>
[
    JsonPolymorphic(TypeDiscriminatorPropertyName = "$type"),
    JsonDerivedType(typeof(StatusChanged), nameof(StatusChanged)),
    JsonDerivedType(typeof(FailureOccurred), nameof(FailureOccurred))
]
public abstract record DaemonEvent;

internal sealed record StatusChanged(Status NewStatus) : DaemonEvent;

internal sealed record FailureOccurred(Exception Exception) : DaemonEvent;
