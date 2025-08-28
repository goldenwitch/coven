using Microsoft.CodeAnalysis;

namespace Coven.Analyzers.Helpers;

internal static class SymbolHelpers
{
    internal static bool HasMutableInstanceState(INamedTypeSymbol type)
    {
        // Scaffold: detect settable properties or non-readonly fields on instance
        foreach (var m in type.GetMembers())
        {
            if (m.IsStatic) continue;
            if (m is IFieldSymbol f && !f.IsReadOnly) return true;
            if (m is IPropertySymbol p && !p.IsReadOnly) return true;
        }
        return false;
    }

    internal static bool UsesKnownNonDeterministicApis(SemanticModel model, SyntaxNode node)
    {
        // Scaffold: hook for future flow analysis; returns false for now
        return false;
    }
}

