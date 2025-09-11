// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Coven.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class COV002_StaticTagSelectorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COV002";

    private static readonly LocalizableString Title = "Selection strategies must be stateless and deterministic";
    private static readonly LocalizableString Message = "ISelectionStrategy implementations should be stateless and deterministic";
    private static readonly LocalizableString Description = "Implementations of ISelectionStrategy should not carry mutable instance state or use non-deterministic APIs.";
    private const string Category = "Design";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        Message,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    // Suggestion for sealing strategy classes
    public static readonly DiagnosticDescriptor SealRule = new(
        "COV002S",
        "Strategy types should be sealed",
        "ISelectionStrategy implementations should be sealed to keep behavior predictable",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, SealRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startCtx =>
        {
            var iSelectionStrategy = startCtx.Compilation.GetTypeByMetadataName("Coven.Core.Routing.ISelectionStrategy");
            var ifaceName = "ISelectionStrategy";
            var ifaceNamespace = "Coven.Core.Routing";

            startCtx.RegisterSymbolAction(symCtx =>
            {
                var type = (INamedTypeSymbol)symCtx.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;
                if (!Implements(type, iSelectionStrategy, ifaceName, ifaceNamespace))
                    return;

                // 1) Statefulness: mutable instance fields or settable properties
                foreach (var m in type.GetMembers())
                {
                    if (m.IsStatic) continue;
                    switch (m)
                    {
                        case IFieldSymbol f when !f.IsReadOnly:
                            symCtx.ReportDiagnostic(Diagnostic.Create(Rule, f.Locations.FirstOrDefault(), Array.Empty<object?>()));
                            break;
                        case IPropertySymbol p:
                            if (p.SetMethod is null) break;
                            // Allow init-only setters, flag others
                            if (!(p.SetMethod.IsInitOnly))
                            {
                                symCtx.ReportDiagnostic(Diagnostic.Create(Rule, p.Locations.FirstOrDefault(), Array.Empty<object?>()));
                            }
                            break;
                    }
                }

                // 3) Suggest sealing
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsSealed)
                {
                    symCtx.ReportDiagnostic(Diagnostic.Create(SealRule, type.Locations.FirstOrDefault(), Array.Empty<object?>()));
                }
            }, SymbolKind.NamedType);

            // 2) Non-determinism: inside SelectNext method body
            startCtx.RegisterOperationBlockStartAction(blockStartCtx =>
            {
                if (blockStartCtx.OwningSymbol is not IMethodSymbol method) return;
                if (method.MethodKind != MethodKind.Ordinary) return;
                var containing = method.ContainingType;
                if (!Implements(containing, iSelectionStrategy, ifaceName, ifaceNamespace)) return;
                if (!string.Equals(method.Name, "SelectNext", System.StringComparison.Ordinal)) return;

                blockStartCtx.RegisterOperationAction(opCtx =>
                {
                    var inv = (IInvocationOperation)opCtx.Operation;
                    var target = inv.TargetMethod;
                    if (IsDisallowedInvocation(target))
                    {
                        opCtx.ReportDiagnostic(Diagnostic.Create(Rule, inv.Syntax.GetLocation()));
                    }
                }, OperationKind.Invocation);

                blockStartCtx.RegisterOperationAction(opCtx =>
                {
                    var obj = (IObjectCreationOperation)opCtx.Operation;
                    var type = obj.Constructor?.ContainingType;
                    if (type is null) return;
                    if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Random")
                    {
                        opCtx.ReportDiagnostic(Diagnostic.Create(Rule, obj.Syntax.GetLocation()));
                    }
                }, OperationKind.ObjectCreation);

                blockStartCtx.RegisterOperationAction(opCtx =>
                {
                    var prop = (IPropertyReferenceOperation)opCtx.Operation;
                    if (IsDisallowedProperty(prop.Property))
                    {
                        opCtx.ReportDiagnostic(Diagnostic.Create(Rule, prop.Syntax.GetLocation()));
                    }
                }, OperationKind.PropertyReference);
            });
        });
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? ifaceSymbol, string ifaceSimpleName, string ifaceNamespace)
    {
        foreach (var i in type.AllInterfaces)
        {
            if (ifaceSymbol is not null && SymbolEqualityComparer.Default.Equals(i, ifaceSymbol))
                return true;
            // Fallback by name/namespace for sources that define the interface in-compilation (tests)
            if (i.Name == ifaceSimpleName && i.ContainingNamespace?.ToDisplayString() == ifaceNamespace)
                return true;
        }
        return false;
    }

    private static bool IsDisallowedInvocation(IMethodSymbol method)
    {
        var containing = method.ContainingType;
        var typeName = containing.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = method.Name;

        // Guid.NewGuid
        if (typeName == "global::System.Guid" && name == "NewGuid") return true;
        // Stopwatch.StartNew
        if (typeName == "global::System.Diagnostics.Stopwatch" && name == "StartNew") return true;
        // Random.Next* (method invocations)
        if (typeName == "global::System.Random" && name.StartsWith("Next", System.StringComparison.Ordinal)) return true;
        // Environment.TickCount/TickCount64 via property get methods
        if (typeName == "global::System.Environment" && (name == "get_TickCount" || name == "get_TickCount64")) return true;
        return false;
    }

    private static bool IsDisallowedProperty(IPropertySymbol property)
    {
        var containing = property.ContainingType;
        var typeName = containing.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = property.Name;
        // DateTime.Now/UtcNow/Today
        if (typeName == "global::System.DateTime" && (name == "Now" || name == "UtcNow" || name == "Today")) return true;
        // DateTimeOffset.Now/UtcNow
        if (typeName == "global::System.DateTimeOffset" && (name == "Now" || name == "UtcNow")) return true;
        // Environment.TickCount/TickCount64 are surfaced as properties in syntax, but we also catch invocation form above
        if (typeName == "global::System.Environment" && (name == "TickCount" || name == "TickCount64")) return true;
        return false;
    }
}