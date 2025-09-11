// SPDX-License-Identifier: BUSL-1.1

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Coven.Analyzers.Tests;

public class COV003_TagScopeUsageTests
{
    private static CSharpAnalyzerTest<Rules.COV003_TagScopeUsageAnalyzer, DefaultVerifier> CreateTest(string source)
    {
        var test = new CSharpAnalyzerTest<Rules.COV003_TagScopeUsageAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };
        return test;
    }

    private const string Scaffold = @"
namespace Coven.Core
{
    public interface IMagikBlock<TIn, TOut>
    {
        TOut DoMagik(TIn input);
    }
}
namespace Coven.Core.Tags
{
    public static class Tag
    {
        public static System.Collections.Generic.ISet<string> Current => null;
        public static void Add(string tag) { }
        public static bool Contains(string tag) => false;
    }
}
";

    [Fact]
    public async Task Tag_Usage_In_Constructor_Is_Flagged()
    {
        var src = Scaffold + @"
public sealed class Bad : Coven.Core.IMagikBlock<int, int>
{
    public Bad() { {|#0:Coven.Core.Tags.Tag.Add(""by:ctor"")|}; }
    public int DoMagik(int input) => input;
}
";
        var test = CreateTest(src);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV003_TagScopeUsageAnalyzer.Rule).WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_In_Field_Initializer_Is_Flagged()
    {
        var src = Scaffold + @"
public sealed class Bad2 : Coven.Core.IMagikBlock<int, int>
{
    private readonly bool _b = {|#0:Coven.Core.Tags.Tag.Contains(""x"")|};
    public int DoMagik(int input) => input;
}
";
        var test = CreateTest(src);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV003_TagScopeUsageAnalyzer.Rule).WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_Inside_DoMagik_Is_Allowed()
    {
        var src = Scaffold + @"
public sealed class Good : Coven.Core.IMagikBlock<int, int>
{
    public int DoMagik(int input)
    {
        Coven.Core.Tags.Tag.Add(""ok"");
        return input;
    }
}
";
        var test = CreateTest(src);
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_In_Local_Function_Is_Allowed()
    {
        var src = Scaffold + @"
public sealed class Good2 : Coven.Core.IMagikBlock<int, int>
{
    public int DoMagik(int input)
    {
        int Helper(int x) { Coven.Core.Tags.Tag.Add(""okLF""); return x; }
        return Helper(input);
    }
}
";
        var test = CreateTest(src);
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_In_Lambda_Is_Allowed()
    {
        var src = Scaffold + @"
public sealed class Good3 : Coven.Core.IMagikBlock<int, int>
{
    public int DoMagik(int input)
    {
        System.Func<int,int> f = x => { Coven.Core.Tags.Tag.Add(""okL""); return x; };
        return f(input);
    }
}
";
        var test = CreateTest(src);
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_In_Helper_Method_Reachable_From_DoMagik_Is_Allowed()
    {
        var src = Scaffold + @"
public sealed class Good4 : Coven.Core.IMagikBlock<int, int>
{
    public int DoMagik(int input)
    {
        return Helper(input);
    }
    private int Helper(int x)
    {
        Coven.Core.Tags.Tag.Add(""okH"");
        return x;
    }
}
";
        var test = CreateTest(src);
        await test.RunAsync();
    }

    [Fact]
    public async Task Tag_Usage_In_Unreachable_Helper_Is_Flagged()
    {
        var src = Scaffold + @"
public sealed class Bad3 : Coven.Core.IMagikBlock<int, int>
{
    public int DoMagik(int input) => input;
    private int NotCalled(int x) { {|#0:Coven.Core.Tags.Tag.Add(""bad"")|}; return x; }
}
";
        var test = CreateTest(src);
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Rules.COV003_TagScopeUsageAnalyzer.Rule).WithLocation(0));
        await test.RunAsync();
    }
}