// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting;
using Coven.Spellcasting.Spells;
using Xunit;

namespace Coven.Spellcasting.Tests;

public sealed class SpellbookBuilderTests
{
    // Dummy shapes for testing
    public sealed record In1(string Value);
    public sealed record In2(int A, int B);
    public sealed record Out2(int Sum);

    // Zero-arg spell
    private sealed class ZeroSpell : ISpell
    {
        public Task CastSpell() => Task.CompletedTask;
    }

    // Unary spell
    private sealed class UnarySpell : ISpell<In1>
    {
        public Task CastSpell(In1 Input) => Task.CompletedTask;
    }

    // Binary spell
    private sealed class BinarySpell : ISpell<In2, Out2>
    {
        public Task<Out2> CastSpell(In2 Input) => Task.FromResult(new Out2(Input.A + Input.B));
    }

    [Fact]
    public void Build_Adds_Definitions_And_Spells()
    {
        var book = new Coven.Spellcasting.SpellbookBuilder()
            .AddSpell(new ZeroSpell())
            .AddSpell<In1>(new UnarySpell())
            .AddSpell<In2, Out2>(new BinarySpell())
            .Build();

        Assert.Equal(3, book.Definitions.Count);
        Assert.Equal(3, book.Spells.Count);

        var names = book.Definitions.Select(d => d.Name).ToHashSet();
        Assert.Contains(SchemaGen.GetFriendlyName(typeof(ZeroSpell)), names);
        Assert.Contains(SchemaGen.GetFriendlyName(typeof(In1)), names);
        Assert.Contains(SchemaGen.GetFriendlyName(typeof(In2)), names);

        var zero = book.Definitions.First(d => d.Name == SchemaGen.GetFriendlyName(typeof(ZeroSpell)));
        Assert.Null(zero.InputSchema);
        Assert.Null(zero.OutputSchema);

        var unary = book.Definitions.First(d => d.Name == SchemaGen.GetFriendlyName(typeof(In1)));
        Assert.NotNull(unary.InputSchema);
        Assert.Null(unary.OutputSchema);

        var binary = book.Definitions.First(d => d.Name == SchemaGen.GetFriendlyName(typeof(In2)));
        Assert.NotNull(binary.InputSchema);
        Assert.NotNull(binary.OutputSchema);
    }
}