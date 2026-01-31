// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Core.Covenants;

/// <summary>
/// A pre-compiled pump that can be executed with a service provider.
/// All type information is captured in the factory closure.
/// </summary>
public sealed record PumpDescriptor(
    Type SourceType,
    Type TargetType,
    Func<IServiceProvider, CancellationToken, Task> CreatePump);
