using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coven.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class COV002_StaticTagSelectorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COV002";

    private static readonly LocalizableString Title = "Selection strategies must be stateless and deterministic";
    private static readonly LocalizableString Message = "ISelectionStrategy implementations should be stateless and deterministic";
    private static readonly LocalizableString Description = "Implementations of ISelectionStrategy should not carry mutable instance state or use non-deterministic APIs.";
    private const string Category = "Design";

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        Message,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Scaffold only: we’ll flesh out checks in a later pass.
        context.RegisterCompilationStartAction(startCtx =>
        {
            var iSelectionStrategy = startCtx.Compilation.GetTypeByMetadataName("Coven.Core.Routing.ISelectionStrategy");
            if (iSelectionStrategy is null)
                return;

            startCtx.RegisterSymbolAction(symCtx =>
            {
                var type = (INamedTypeSymbol)symCtx.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;
                if (!Implements(type, iSelectionStrategy))
                    return;

                // Placeholder diagnostic to prove wiring (will be gated by real checks later)
                // For now, do not report by default to avoid noise. Leave scaffolding in place.
                // symCtx.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations.FirstOrDefault()));
            }, SymbolKind.NamedType);
        });
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol iface)
    {
        foreach (var i in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(i, iface)) return true;
        }
        return false;
    }
}

