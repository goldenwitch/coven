// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;
using Xunit;

namespace Coven.Core.Tests;

public class HaloPullTests
{
    // Shared minimal types from the Halo E2E to lock behavior shape
    private sealed class Doc { public string Text { get; init; } = string.Empty; }

    private sealed class ParseAndTag : IMagikBlock<string, Doc>
    {
        public Task<Doc> DoMagik(string input, CancellationToken cancellationToken = default)
        {
            if (input.Contains('!')) Tag.Add("exclaim");
            Tag.Add("style:loud");
            return Task.FromResult(new Doc { Text = input });
        }
    }

    private sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
    {
        public IReadOnlyCollection<string> SupportedTags => new[] { "exclaim" };
        public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default)
            => Task.FromResult(new Doc { Text = $"☀ PRAISE THE SUN! ☀ {input.Text}" });
    }

    private sealed class UppercaseText : IMagikBlock<Doc, Doc>
    {
        public Task<Doc> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
    }

    private sealed class ToOut : IMagikBlock<Doc, string>
    {
        public Task<string> DoMagik(Doc input, CancellationToken cancellationToken = default) => Task.FromResult(input.Text);
    }

    [Fact]
    public async Task Halo_Pull_EndToEnd_WithBuilderAndRitual()
    {
        // Build Coven in Pull mode using the MagikBuilder
        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new ParseAndTag())
            .MagikBlock(new AddSalutation())
            .MagikBlock(new UppercaseText(), new[] { "style:loud" })
            .MagikBlock(new ToOut())
            .Done(pull: true, pullOptions: new PullOptions
            {
                // Only consider the ritual complete upfront if the input already
                // matches the intended final form (contains the salutation and is uppercase).
                ShouldComplete = o => o is string s &&
                                         s.Contains("PRAISE THE SUN", StringComparison.OrdinalIgnoreCase) &&
                                         s == s.ToUpperInvariant()
            });

        var input = "hello coven!!! let's test tags";
        var output = await coven.Ritual<string, string>(input);

        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }
}
