using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class HaloE2ETests
{
    private sealed class Doc { public string Text { get; init; } = string.Empty; }

    private sealed class ParseAndTag : IMagikBlock<string, Doc>
    {
        public Task<Doc> DoMagik(string input)
        {
            // Tag based on content; exclamation -> exclaim, short vs long, loud for this demo
            if (input.Contains('!')) Tag.Add("exclaim");
            Tag.Add("style:loud");
            return Task.FromResult(new Doc { Text = input });
        }
    }

    private sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "exclaim" };
        public Task<Doc> DoMagik(Doc input)
        {
            // Praise the Sun easter egg
            var text = $"☀ PRAISE THE SUN! ☀ {input.Text} — If only I could be so grossly incandescent.";
            return Task.FromResult(new Doc { Text = text });
        }
    }

    private sealed class UppercaseText : IMagikBlock<Doc, Doc>
    {
        public Task<Doc> DoMagik(Doc input) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
    }

    private sealed class LowercaseText : IMagikBlock<Doc, Doc>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "style:quiet" };
        public Task<Doc> DoMagik(Doc input) => Task.FromResult(new Doc { Text = input.Text.ToLowerInvariant() });
    }

    private sealed class ToOut : IMagikBlock<Doc, string>
    {
        public Task<string> DoMagik(Doc input) => Task.FromResult(input.Text);
    }

    [Fact]
    public async Task Halo_EndToEnd_CapabilityRouting_UppercaseSalutation()
    {
        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new ParseAndTag())                       // idx 0: string -> Doc (emits exclaim, style:loud)
            .MagikBlock<Doc, Doc>(new AddSalutation())           // idx 1: Doc -> Doc (cap: exclaim)
            .MagikBlock<Doc, Doc>(new UppercaseText(), new[] { "style:loud" }) // idx 2: Doc -> Doc (builder cap: loud)
            .MagikBlock<Doc, string>(new ToOut())                // idx 3: Doc -> string (placed before Lowercase to stop)
            .MagikBlock<Doc, Doc>(new LowercaseText())           // idx 4: Doc -> Doc (cap: quiet)
            .Done();

        var input = "hello coven!!! let's test tags";
        var output = await coven.Ritual<string, string>(input);

        // Uppercased sun praise and phrase should be present
        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("IF ONLY I COULD BE SO GROSSLY INCANDESCENT", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }

    [Fact]
    public async Task Halo_EndToEnd_ExplicitOverride_ToLowercase()
    {
        // Build board directly to pass tags to PostWork (ICoven doesn't carry tags)
        var registry = new List<MagikBlockDescriptor>
        {
            new(typeof(string), typeof(Doc), new ParseAndTag()),                     // idx 0
            new(typeof(Doc), typeof(Doc), new AddSalutation()),                      // idx 1
            new(typeof(Doc), typeof(Doc), new UppercaseText(), new[] { "style:loud" }), // idx 2
            new(typeof(Doc), typeof(Doc), new LowercaseText()),                      // idx 3
            new(typeof(Doc), typeof(string), new ToOut()),                            // idx 4 (after lowercase to allow override path)
        };

        var boardType = typeof(Board);
        var ctor = boardType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            new[] { boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!, typeof(IReadOnlyList<MagikBlockDescriptor>) },
            modifiers: null
        );
        var boardModeType = boardType.GetNestedType("BoardMode", System.Reflection.BindingFlags.NonPublic)!;
        var pushEnum = Enum.Parse(boardModeType, "Push");
        var board = (Board)ctor!.Invoke(new object?[] { pushEnum, registry });

        var input = "Hello Coven!!!";
        var output = await board.PostWork<string, string>(input, new List<string> { "to:AddSalutation", "to:LowercaseText" });

        var lower = output.ToLowerInvariant();
        Assert.Equal(output, lower); // ensure fully lowercase
        Assert.Contains("praise the sun", lower);
        Assert.Contains("if only i could be so grossly incandescent", lower);
    }
}
