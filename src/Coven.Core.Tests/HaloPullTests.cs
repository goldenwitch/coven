// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Tags;
using Coven.Core.Di;
using Microsoft.Extensions.DependencyInjection;
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
        // Build Coven in Pull mode via DI
        var services = new ServiceCollection();
        services.BuildCoven(c =>
        {
            c.AddBlock<string, Doc, ParseAndTag>();
            c.AddBlock<Doc, Doc, AddSalutation>();
            c.AddBlock<Doc, Doc>(sp => new UppercaseText(), capabilities: new[] { "style:loud" });
            c.AddBlock<Doc, string, ToOut>();
            c.Done(pull: true, pullOptions: new PullOptions
            {
                ShouldComplete = o => o is string s &&
                                         s.Contains("PRAISE THE SUN", StringComparison.OrdinalIgnoreCase) &&
                                         s == s.ToUpperInvariant()
            });
        });
        using var sp = services.BuildServiceProvider();
        var coven = sp.GetRequiredService<ICoven>();

        var input = "hello coven!!! let's test tags";
        var output = await coven.Ritual<string, string>(input);

        Assert.Contains("PRAISE THE SUN", output);
        Assert.Contains("HELLO COVEN!!! LET'S TEST TAGS".ToUpperInvariant(), output);
    }
}
