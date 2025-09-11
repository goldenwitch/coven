// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Xunit;

namespace Coven.Core.Tests;

public class PullBehaviorTests
{
    // Assert that declared generic TOut controls finality in pull mode,
    // not the runtime type of the value produced by earlier steps.
    private sealed class Start { public string Value { get; init; } = string.Empty; }

    private sealed class ToObject : IMagikBlock<Start, object>
    {
        public Task<object> DoMagik(Start input) => Task.FromResult((object)input.Value);
    }

    private sealed class ObjectToStringPlus : IMagikBlock<object, string>
    {
        public Task<string> DoMagik(object input) => Task.FromResult(((string)input) + "|b2");
    }

    [Fact]
    public async Task Pull_DeclaredType_Drives_Finality_Not_Runtime()
    {
        var coven = new MagikBuilder<Start, string>()
            .MagikBlock(new ToObject())                           // Start -> object (runtime is string)
            .MagikBlock<object, string>(new ObjectToStringPlus()) // object -> string (must still run)
            .Done(pull: true);

        var result = await coven.Ritual<Start, string>(new Start { Value = "x" });
        Assert.Equal("x|b2", result);
    }
}