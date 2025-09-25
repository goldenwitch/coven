// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Builder;

// Ambient access to the current ritual's IServiceProvider. Scoped per ritual via AsyncLocal.
internal static class CovenExecutionScope
{
    private static readonly AsyncLocal<IServiceScope?> _currentScope = new();

    internal static IServiceProvider? CurrentProvider => _currentScope.Value?.ServiceProvider;

    internal static IServiceScope? BeginScope(IServiceProvider root)
    {
        IServiceScopeFactory scopeFactory = root.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Coven DI: IServiceScopeFactory not available on the root provider.");
        IServiceScope scope = scopeFactory.CreateScope();
        _currentScope.Value = scope;
        return scope;
    }

    internal static void EndScope(IServiceScope? prev)
    {
        try { prev?.Dispose(); }
        finally { _currentScope.Value = null; }
    }
}