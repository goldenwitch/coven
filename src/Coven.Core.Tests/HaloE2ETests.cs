// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Coven.Core.Tests;

public class HaloE2ETests
{
    private sealed class Doc { public string Text { get; init; } = string.Empty; }

    private sealed class ParseAndTag : IMagikBlock<string, Doc>
    {
        public Task<Doc> DoMagik(string input, CancellationToken cancellationToken = default)
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
        public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default)
        {
            // Praise the Sun easter egg
            var text = $"☀ PRAISE THE SUN! ☀ {input.Text} — If only I could be so grossly incandescent.";
            return Task.FromResult(new Doc { Text = text });
        }
    }

    private sealed class UppercaseText : IMagikBlock<Doc, Doc>
    {
        public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
    }

    private sealed class LowercaseText : IMagikBlock<Doc, Doc>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "style:quiet" };
        public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToLowerInvariant() });
    }

    private sealed class ToOut : IMagikBlock<Doc, string>
    {
        public Task<string> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(input.Text);
    }

    [Fact]
    public async Task Halo_EndToEnd_CapabilityRouting_UppercaseSalutation()
    {
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, Doc, ParseAndTag>();
            c.AddBlock<Doc, Doc, AddSalutation>();
            c.AddBlock<Doc, Doc>(sp => new UppercaseText(), capabilities: new[] { "style:loud" });
            c.AddBlock<Doc, string, ToOut>();
            c.AddBlock<Doc, Doc, LowercaseText>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

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
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, Doc, ParseAndTag>();
            c.AddBlock<Doc, Doc, AddSalutation>();
            c.AddBlock<Doc, Doc>(sp => new UppercaseText(), capabilities: new[] { "style:loud" });
            c.AddBlock<Doc, Doc, LowercaseText>();
            c.AddBlock<Doc, string, ToOut>();
            c.Done();
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var input = "Hello Coven!!!";
        var output = await coven.Ritual<string, string>(input, new List<string> { "to:AddSalutation", "to:LowercaseText" });

        var lower = output.ToLowerInvariant();
        Assert.Equal(output, lower); // ensure fully lowercase
        Assert.Contains("praise the sun", lower);
        Assert.Contains("if only i could be so grossly incandescent", lower);
    }
}
