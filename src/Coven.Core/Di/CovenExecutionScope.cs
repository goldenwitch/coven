using Microsoft.Extensions.DependencyInjection;

namespace Coven.Core.Di;

// Ambient access to the current ritual's IServiceProvider. Scoped per ritual via AsyncLocal.
internal static class CovenExecutionScope
{
    private static readonly AsyncLocal<IServiceScope?> CurrentScope = new();

    internal static IServiceProvider? CurrentProvider => CurrentScope.Value?.ServiceProvider;

    internal static IServiceScope? BeginScope(IServiceProvider root)
    {
        var scopeFactory = root.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Coven DI: IServiceScopeFactory not available on the root provider.");
        var scope = scopeFactory.CreateScope();
        CurrentScope.Value = scope;
        return scope;
    }

    internal static void EndScope(IServiceScope? prev)
    {
        try { prev?.Dispose(); }
        finally { CurrentScope.Value = null; }
    }
}
