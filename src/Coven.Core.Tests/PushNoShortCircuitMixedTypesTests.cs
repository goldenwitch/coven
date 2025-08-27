using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class PushNoShortCircuitMixedTypesTests
{
    [Fact]
    public async Task Push_MixedTypes_DoesNotShortCircuit_OnAssignable()
    {
        int finalRan = 0;

        var coven = new MagikBuilder<string, string>()
            // Step 1: string -> int
            .MagikBlock<string, int>(s => Task.FromResult(s.Length))
            // Step 2: int -> string (now assignable to TOut)
            .MagikBlock<int, string>(i => Task.FromResult($"len:{i}"))
            // Step 3: string -> string (should still run; no short-circuit)
            .MagikBlock<string, string>(s => { finalRan++; return Task.FromResult(s + "|final"); })
            .Done(); // push mode

        var result = await coven.Ritual<string, string>("abcd");

        Assert.Equal(1, finalRan); // ensure the last step executed
        Assert.Equal("len:4|final", result);
    }
}

