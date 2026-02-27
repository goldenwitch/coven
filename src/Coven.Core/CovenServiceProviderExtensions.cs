// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;

namespace Coven.Core;

/// <summary>
/// Extension methods for daemon-only hosting scenarios.
/// </summary>
public static class CovenServiceProviderExtensions
{
    /// <summary>
    /// Starts all daemons and keeps them running until <paramref name="cancellationToken"/> fires.
    /// Use for long-running, daemon-only scenarios (e.g., interactive console apps)
    /// where there is no pipeline work to run through the board.
    /// </summary>
    /// <param name="services">The root service provider containing the configured coven.</param>
    /// <param name="cancellationToken">Token that signals the daemons should shut down.</param>
    public static async Task Inhabit(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        DaemonScope? scope = await CovenExecutionScope.BeginScopeAsync(services, cancellationToken);

        CovenExecutionScope.SetCurrentScope(scope);
        try
        {
            // Block until cancellation — daemons stay alive
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected — shutdown requested
        }
        finally
        {
            CovenExecutionScope.SetCurrentScope(null);
            await CovenExecutionScope.EndScopeAsync(scope, CancellationToken.None);
        }
    }
}
