// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Coven.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class COV001_ImmutableInputAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COV001";

    private static readonly LocalizableString Title = "MagikBlock input must be immutable";
    private static readonly LocalizableString Message = "IMagikBlock<TIn, TOut> input type must be immutable";
    private static readonly LocalizableString Description = "The input type TIn for IMagikBlock<TIn, TOut> should be an immutable record or a known immutable type.";
    private const string Category = "Design";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        Message,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startCtx =>
        {
            var iMagikBlock = startCtx.Compilation.GetTypeByMetadataName("Coven.Core.IMagikBlock`2");
            startCtx.RegisterSymbolAction(symCtx =>
            {
                var type = (INamedTypeSymbol)symCtx.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;

                foreach (var iface in type.AllInterfaces)
                {
                    if (iMagikBlock is not null && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, iMagikBlock))
                    {
                        if (iface.TypeArguments.Length >= 1)
                        {
                            var tIn = iface.TypeArguments[0];
                            if (!IsImmutable(tIn))
                            {
                                var loc = tIn.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault();
                                if (loc is not null)
                                {
                                    symCtx.ReportDiagnostic(Diagnostic.Create(Rule, loc));
                                }
                            }
                        }
                    }
                }
            }, SymbolKind.NamedType);
        });
    }

    private static bool IsImmutable(ITypeSymbol type)
    {
        // Primitive and BCL immutables
        if (type is { SpecialType: SpecialType.System_String }) return true;
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return true;
        }

        if (type is INamedTypeSymbol named)
        {
            var fullName = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName is "global::System.Guid" or "global::System.DateTime" or "global::System.DateTimeOffset" or "global::System.TimeSpan")
                return true;

            // Attribute override: [CovenImmutable]
            foreach (var attr in named.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;
                if (attrName is "CovenImmutable" or "CovenImmutableAttribute")
                    return true;
            }

            // Records: ensure get-only or init-only and readonly fields
            if (named.IsRecord)
            {
                if (!HasMutableMembers(named))
                    return true;
                return false;
            }

            // Structs: require all instance fields readonly and properties get-only or init
            if (named.IsValueType)
            {
                if (!HasMutableMembers(named))
                    return true;
            }
        }

        return false;
    }

    private static bool HasMutableMembers(INamedTypeSymbol type)
    {
        foreach (var m in type.GetMembers())
        {
            if (m.IsStatic) continue;
            if (m.IsImplicitlyDeclared) continue; // ignore compiler-synthesized
            switch (m)
            {
                case IFieldSymbol f when !f.IsReadOnly:
                    return true;
                case IPropertySymbol p:
                    if (p.SetMethod is null) break;
                    if (!p.SetMethod.IsInitOnly) return true; // allow init-only
                    break;
            }
        }
        return false;
    }
}
