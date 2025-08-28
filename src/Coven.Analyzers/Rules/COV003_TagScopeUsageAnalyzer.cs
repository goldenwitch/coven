using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Coven.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class COV003_TagScopeUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COV003";

    private static readonly LocalizableString Title = "Tag API usage must be inside DoMagik scope";
    private static readonly LocalizableString Message = "Calls to Tag API must occur inside IMagikBlock.DoMagik";
    private static readonly LocalizableString Description = "Tags are scoped per request. Accessing Coven.Core.Tags.Tag outside IMagikBlock.DoMagik may run without an active tag scope.";
    private const string Category = "Usage";

    public static readonly DiagnosticDescriptor Rule = new(
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

        context.RegisterCompilationStartAction(static startCtx =>
        {
            var tagType = startCtx.Compilation.GetTypeByMetadataName("Coven.Core.Tags.Tag");
            var iMagik = startCtx.Compilation.GetTypeByMetadataName("Coven.Core.IMagikBlock`2");

            var typeData = new ConcurrentDictionary<INamedTypeSymbol, TypeState>(SymbolEqualityComparer.Default);

            // Immediate checks for Tag API used outside method bodies (e.g., field initializers)
            startCtx.RegisterOperationAction(opCtx =>
            {
                var inv = (IInvocationOperation)opCtx.Operation;
                if (!IsTagApi(inv.TargetMethod.ContainingType, tagType)) return;
                if (opCtx.ContainingSymbol is not IMethodSymbol)
                {
                    opCtx.ReportDiagnostic(Diagnostic.Create(Rule, inv.Syntax.GetLocation()));
                }
            }, OperationKind.Invocation);

            startCtx.RegisterOperationAction(opCtx =>
            {
                var prop = (IPropertyReferenceOperation)opCtx.Operation;
                if (!IsTagApi(prop.Property.ContainingType, tagType)) return;
                if (opCtx.ContainingSymbol is not IMethodSymbol)
                {
                    opCtx.ReportDiagnostic(Diagnostic.Create(Rule, prop.Syntax.GetLocation()));
                }
            }, OperationKind.PropertyReference);

            startCtx.RegisterSymbolStartAction(symbolStartCtx =>
            {
                if (symbolStartCtx.Symbol is not INamedTypeSymbol type) return;

                var data = new TypeState(type);
                typeData[type] = data;

                symbolStartCtx.RegisterOperationBlockStartAction(blockStartCtx =>
                {
                    if (blockStartCtx.OwningSymbol is not IMethodSymbol owner) return;
                    // Record DoMagik methods
                    if (string.Equals(owner.Name, "DoMagik", System.StringComparison.Ordinal) && ImplementsIMagik(type, iMagik))
                    {
                        data.DoMagikMethods.Add(owner);
                    }

                    // Capture invocations for edges and Tag candidates
                    blockStartCtx.RegisterOperationAction(opCtx =>
                    {
                        var inv = (IInvocationOperation)opCtx.Operation;

                        // Edge within same type
                        var target = inv.TargetMethod;
                        if (SymbolEqualityComparer.Default.Equals(target.ContainingType, type))
                        {
                            if (!data.Edges.TryGetValue(owner, out var set))
                            {
                                set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                                data.Edges[owner] = set;
                            }
                            set.Add(target);
                        }

                        // Tag API usage candidate
                        if (IsTagApi(target.ContainingType, tagType))
                        {
                            data.Candidates.Add((owner, inv.Syntax.GetLocation()));
                        }
                    }, OperationKind.Invocation);

                    // Property references like Tag.Current
                    blockStartCtx.RegisterOperationAction(opCtx =>
                    {
                        var prop = (IPropertyReferenceOperation)opCtx.Operation;
                        if (IsTagApi(prop.Property.ContainingType, tagType))
                        {
                            data.Candidates.Add((owner, prop.Syntax.GetLocation()));
                        }
                    }, OperationKind.PropertyReference);
                });

                symbolStartCtx.RegisterSymbolEndAction(symbolEndCtx =>
                {
                    // Compute reachability from DoMagik
                    var reachable = new HashSet<IMethodSymbol>(data.DoMagikMethods, SymbolEqualityComparer.Default);
                    var queue = new Queue<IMethodSymbol>(data.DoMagikMethods);
                    while (queue.Count > 0)
                    {
                        var m = queue.Dequeue();
                        if (!data.Edges.TryGetValue(m, out var nexts)) continue;
                        foreach (var n in nexts)
                        {
                            if (reachable.Add(n)) queue.Enqueue(n);
                        }
                    }

                    // Report candidates not within reachable
                    foreach (var (owner, loc) in data.Candidates)
                    {
                        if (reachable.Contains(owner)) continue;
                        symbolEndCtx.ReportDiagnostic(Diagnostic.Create(Rule, loc));
                    }
                });
            }, SymbolKind.NamedType);
        });
    }

    private static bool IsTagApi(INamedTypeSymbol? containingType, INamedTypeSymbol? tagType)
    {
        if (containingType is null) return false;
        if (tagType is not null && SymbolEqualityComparer.Default.Equals(containingType, tagType)) return true;
        // Fallback by name/namespace
        return containingType.Name == "Tag" && containingType.ContainingNamespace?.ToDisplayString() == "Coven.Core.Tags";
    }

    private static bool ImplementsIMagik(INamedTypeSymbol type, INamedTypeSymbol? iMagik)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface is INamedTypeSymbol named && named.Arity == 2)
            {
                if (iMagik is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, iMagik))
                    return true;
                if (iface.Name == "IMagikBlock" && iface.ContainingNamespace?.ToDisplayString() == "Coven.Core")
                    return true;
            }
        }
        return false;
    }

    private sealed class TypeState
    {
        public INamedTypeSymbol Type { get; }
        public readonly Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> Edges = new(SymbolEqualityComparer.Default);
        public readonly HashSet<IMethodSymbol> DoMagikMethods = new(SymbolEqualityComparer.Default);
        public readonly List<(IMethodSymbol Owner, Location Loc)> Candidates = new();
        public TypeState(INamedTypeSymbol type) { Type = type; }
    }
}
