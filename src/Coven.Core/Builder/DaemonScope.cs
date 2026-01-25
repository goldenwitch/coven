// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Builder;

/// <summary>
/// Holds the service scope, resolved daemons, and cancellation token source for a ritual's execution.
/// Disposing this scope cancels the token and shuts down daemons in reverse startup order.
/// </summary>
internal sealed record DaemonScope(
    IServiceScope Scope,
    IReadOnlyList<IDaemon> Daemons,
    CancellationTokenSource Cts) : IAsyncDisposable
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CovenExecutionScope.EndScopeAsync(this, CancellationToken.None).ConfigureAwait(false);
    }
}
