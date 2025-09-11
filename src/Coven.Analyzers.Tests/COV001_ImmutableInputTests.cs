// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Coven.Analyzers.Tests;

public class COV001_ImmutableInputTests
{
    private static CSharpAnalyzerTest<Rules.COV001_ImmutableInputAnalyzer, DefaultVerifier> CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<Rules.COV001_ImmutableInputAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test;
    }

    private const string Scaffold = @"
namespace Coven.Core
{
    public interface IMagikBlock<TIn, TOut> { }
}
";

    [Fact]
    public async Task Mutable_Class_Input_Is_Flagged()
    {
        var src = Scaffold + @"
public class {|#0:Input|} { public string Name { get; set; } }
public class Block : Coven.Core.IMagikBlock<Input, int> { }
";

        var test = CreateTest(src);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV001_ImmutableInputAnalyzer.Rule).WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Record_Input_Is_Ok()
    {
        var src = Scaffold + @"
public record Input(string Name);
public class Block : Coven.Core.IMagikBlock<Input, int> { }
";

        var test = CreateTest(src);
        await test.RunAsync();
    }
}