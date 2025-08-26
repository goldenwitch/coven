using System;
using System.Collections.Generic;

namespace Coven.Core.Tests;

internal static class TestBoardFactory
{
    // Build a push-mode Board from descriptors using the internal constructor.
    internal static Board NewPushBoard(params MagikBlockDescriptor[] descriptors)
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
}

