using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Coven.Core.Tags;

namespace Coven.Core.Tests;

public class TagScopeTests
{
    private static Board NewPushBoard(params MagikBlockDescriptor[] descriptors)
    {
        var registry = new List<MagikBlockDescriptor>(descriptors);
        var boardType = typeof(Board);
        var ctor = boardType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!, typeof(IReadOnlyList<MagikBlockDescriptor>) },
            modifiers: null
        );
        var boardModeType = boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!;
        var pushEnum = Enum.Parse(boardModeType, "Push");
        return (Board)ctor!.Invoke(new object?[] { pushEnum, registry });
    }

    [Fact]
    public void Tag_Methods_OutsideScope_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => Tag.Add("x"));
        Assert.Throws<InvalidOperationException>(() => Tag.Contains("x"));
        Assert.Throws<InvalidOperationException>(() => { var _ = Tag.Current; });
    }

    private sealed class ProbeBlock : IMagikBlock<string, string>
    {
        public Task<string> DoMagik(string input)
        {
            Tag.Add("probe");
            var ok = Tag.Contains("probe");
            return Task.FromResult(ok ? "ok" : "bad");
        }
    }

    [Fact]
    public async Task Tag_Methods_Available_WithinBoardScope()
    {
        var board = NewPushBoard(new MagikBlockDescriptor(typeof(string), typeof(string), new ProbeBlock()));
        var result = await board.PostWork<string, string>("ignored");
        Assert.Equal("ok", result);
    }
}

