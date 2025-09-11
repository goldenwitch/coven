// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;           // switched from ...Testing.XUnit
using Xunit;

namespace Coven.Analyzers.Tests;

public class COV002_StaticTagSelectorTests
{
    private static CSharpAnalyzerTest<Rules.COV002_StaticTagSelectorAnalyzer, DefaultVerifier>
        CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<Rules.COV002_StaticTagSelectorAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // Optional but recommended to keep test BCL stable:
            // ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test;
    }

    private const string StrategyScaffold = @"
namespace Coven.Core.Routing
{
    public interface ISelectionStrategy
    {
        SelectionCandidate SelectNext(System.Collections.Generic.IReadOnlyList<SelectionCandidate> forward);
    }
    public readonly struct SelectionCandidate { }
}
";

    [Fact]
    public async Task Strategy_With_MutableField_Reports()
    {
        var src = StrategyScaffold + @"
public class {|#0:BadStrategy|} : Coven.Core.Routing.ISelectionStrategy
{
    private int {|#1:counter|};
    public Coven.Core.Routing.SelectionCandidate SelectNext(System.Collections.Generic.IReadOnlyList<Coven.Core.Routing.SelectionCandidate> forward) => default;
}
";

        var test = CreateTest(src);

        test.ExpectedDiagnostics.Clear();
        // Expect the mutability rule on the field and the 'seal' suggestion on the type
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV002_StaticTagSelectorAnalyzer.Rule)
            .WithLocation(1));
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV002_StaticTagSelectorAnalyzer.SealRule)
            .WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task Strategy_With_DateTimeNow_Reports()
    {
        var src = StrategyScaffold + @"
public sealed class BadStrategy2 : Coven.Core.Routing.ISelectionStrategy
{
    public Coven.Core.Routing.SelectionCandidate SelectNext(System.Collections.Generic.IReadOnlyList<Coven.Core.Routing.SelectionCandidate> forward)
    {
        var x = {|#0:System.DateTime.Now|};
        return default;
    }
}
";

        var test = CreateTest(src);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV002_StaticTagSelectorAnalyzer.Rule)
            .WithLocation(0));

        await test.RunAsync();
    }
}