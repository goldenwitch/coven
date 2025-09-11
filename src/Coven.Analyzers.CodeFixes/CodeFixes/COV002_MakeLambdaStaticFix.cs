// SPDX-License-Identifier: BUSL-1.1

using System.Composition;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Coven.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(COV002_MakeLambdaStaticFix))]
[Shared]
public sealed class COV002_MakeLambdaStaticFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Rules.COV002_StaticTagSelectorAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Scaffold only; real code fix logic will be added later once checks are implemented.
        return Task.CompletedTask;
    }
}
