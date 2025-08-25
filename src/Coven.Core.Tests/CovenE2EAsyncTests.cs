using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

namespace Coven.Core.Tests;

public class CovenE2EAsyncTests
{
    [Fact]
    public async Task Ritual_Awaits_MultipleAsyncBlocks_EndToEnd()
    {
        // Compose: string -> int (delay) -> double (delay)
        var b1 = new MagikBlockDescriptor(typeof(string), typeof(int), new AsyncDelayThenLength(40));
        var b2 = new MagikBlockDescriptor(typeof(int), typeof(double), new AsyncDelayThenToDouble(40));

        // Build a Board in push mode (no precompile needed for this test) and a Coven over it
        var boardType = typeof(Board);
        var ctor = boardType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!, typeof(IReadOnlyList<MagikBlockDescriptor>) },
            modifiers: null
        );
        var boardModeType = boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!;
        var pushEnum = System.Enum.Parse(boardModeType, "Push");
        var board = (Board)ctor!.Invoke(new object?[] { pushEnum, new List<MagikBlockDescriptor> { b1, b2 } });

        var coven = new Coven(board); // InternalsVisibleTo allows using internal constructor

        var sw = Stopwatch.StartNew();
        var result = await coven.Ritual<string, double>("abcd");
        sw.Stop();

        Assert.Equal(4d, result);
        Assert.True(sw.ElapsedMilliseconds >= 75, $"Ritual finished too quickly: {sw.ElapsedMilliseconds}ms");
    }

    private sealed class AsyncDelayThenLength : IMagikBlock<string, int>
    {
        private readonly int delayMs;
        public AsyncDelayThenLength(int delayMs) { this.delayMs = delayMs; }
        public async Task<int> DoMagik(string input)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return input.Length;
        }
    }

    private sealed class AsyncDelayThenToDouble : IMagikBlock<int, double>
    {
        private readonly int delayMs;
        public AsyncDelayThenToDouble(int delayMs) { this.delayMs = delayMs; }
        public async Task<double> DoMagik(int input)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return (double)input;
        }
    }
}

