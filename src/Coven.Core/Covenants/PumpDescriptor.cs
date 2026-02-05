// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// A pre-compiled pump that can be executed with a service provider.
/// All type information is captured in the factory closure.
/// </summary>
/// <param name="SourceType">The entry type this pump reads from.</param>
/// <param name="TargetType">The entry type this pump writes to.</param>
/// <param name="CreatePump">Factory that creates a running pump task given a service provider and cancellation token.</param>
public sealed record PumpDescriptor(
    Type SourceType,
    Type TargetType,
    Func<IServiceProvider, CancellationToken, Task> CreatePump);
