// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Builder;

/// <summary>
/// Ambient access to the current ritual's IServiceProvider. Scoped per ritual via AsyncLocal.
/// Manages daemon lifecycle: starts daemons on scope entry, stops them on exit.
/// </summary>
internal static class CovenExecutionScope
{
    private static readonly AsyncLocal<DaemonScope?> _currentScope = new();

    internal static IServiceProvider? CurrentProvider => _currentScope.Value?.Scope.ServiceProvider;

    /// <summary>
    /// Sets the ambient scope. Must be called from the synchronous context that needs to access the scope.
    /// AsyncLocal modifications inside async methods don't propagate to the caller.
    /// </summary>
    internal static void SetCurrentScope(DaemonScope? scope)
    {
        _currentScope.Value = scope;
    }

    /// <summary>
    /// Creates a new scope, resolves all registered daemons, and starts them in order.
    /// If any daemon fails to start, rolls back previously started daemons and throws.
    /// NOTE: Caller must call SetCurrentScope after this returns to establish ambient context.
    /// </summary>
    internal static async Task<DaemonScope> BeginScopeAsync(IServiceProvider root, CancellationToken ct)
    {
        IServiceScopeFactory scopeFactory = root.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Coven DI: IServiceScopeFactory not available on the root provider.");
        IServiceScope scope = scopeFactory.CreateScope();
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        List<IDaemon> daemons = [.. scope.ServiceProvider.GetServices<IDaemon>()];
        DaemonScope daemonScope = new(scope, daemons, cts);

        try
        {
            // Start daemons as part of scope entry
            await StartDaemonsInOrderAsync(daemons, cts.Token).ConfigureAwait(false);
            return daemonScope;
        }
        catch
        {
            cts.Dispose();
            scope.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Cancels the scope's CTS, shuts down daemons in reverse order, then disposes resources.
    /// NOTE: Caller must call SetCurrentScope(null) to clear ambient context.
    /// </summary>
    internal static async Task EndScopeAsync(DaemonScope? daemonScope, CancellationToken ct)
    {
        if (daemonScope is null)
        {
            return;
        }

        try
        {
            // Cancel the scope's CTS first — triggers cooperative shutdown
            // in daemon loops before we call Shutdown()
            await daemonScope.Cts.CancelAsync().ConfigureAwait(false);

            // Now formally shutdown daemons in reverse startup order
            await ShutdownDaemonsAsync(daemonScope.Daemons.Reverse(), ct).ConfigureAwait(false);
        }
        finally
        {
            daemonScope.Cts.Dispose();

            // Use async disposal if available (required for scopes containing IAsyncDisposable services).
            // The Microsoft.Extensions.DependencyInjection scope implements IAsyncDisposable.
            if (daemonScope.Scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                daemonScope.Scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Starts daemons in order. If any fails, rolls back previously started daemons.
    /// </summary>
    private static async Task StartDaemonsInOrderAsync(List<IDaemon> daemons, CancellationToken ct)
    {
        List<IDaemon> started = [];
        try
        {
            foreach (IDaemon daemon in daemons)
            {
                await daemon.Start(ct).ConfigureAwait(false);
                started.Add(daemon);
            }
        }
        catch (Exception ex)
        {
            // Roll back: stop daemons in reverse order
            // Use CancellationToken.None — we need to complete rollback regardless
            foreach (IDaemon daemon in started.AsEnumerable().Reverse())
            {
                try
                {
                    await daemon.Shutdown(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Log but continue rollback
                }
            }

            Type failedDaemon = daemons[started.Count].GetType();
            List<Type> rolledBack = [.. started.Select(d => d.GetType())];
            throw new DaemonStartupException(
                "Scope activation failed: daemon startup error",
                ex,
                failedDaemon,
                rolledBack);
        }
    }

    /// <summary>
    /// Shuts down daemons. Continues even if individual shutdowns fail.
    /// </summary>
    private static async Task ShutdownDaemonsAsync(IEnumerable<IDaemon> daemons, CancellationToken ct)
    {
        foreach (IDaemon daemon in daemons)
        {
            try
            {
                await daemon.Shutdown(ct).ConfigureAwait(false);
            }
            catch
            {
                // Log but continue shutdown of remaining daemons
            }
        }
    }

    #region Backwards compatibility for sync callers

    /// <summary>
    /// Legacy sync scope creation. Does NOT start daemons.
    /// Use BeginScopeAsync for daemon auto-start behavior.
    /// </summary>
    [Obsolete("Use BeginScopeAsync for daemon auto-start. This method does not start daemons.")]
    internal static IServiceScope? BeginScope(IServiceProvider root)
    {
        IServiceScopeFactory scopeFactory = root.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Coven DI: IServiceScopeFactory not available on the root provider.");
        IServiceScope scope = scopeFactory.CreateScope();
        // Store as DaemonScope with empty daemon list for CurrentProvider access
        _currentScope.Value = new DaemonScope(scope, [], new CancellationTokenSource());
        return scope;
    }

    /// <summary>
    /// Legacy sync scope cleanup. Does NOT shut down daemons.
    /// </summary>
    [Obsolete("Use EndScopeAsync for proper daemon shutdown. This method does not shut down daemons.")]
    internal static void EndScope(IServiceScope? scope)
    {
        try
        {
            scope?.Dispose();
            _currentScope.Value?.Cts.Dispose();
        }
        finally
        {
            _currentScope.Value = null;
        }
    }

    #endregion
}