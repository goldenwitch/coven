# End-to-End Examples

`ICoven` exposes a simple API `Ritual<TIn, TOut>(TIn input, List<string>? tags = null)`. The route between `TIn` and `TOut` is decided at runtime by tags and capabilities, with explicit `to:*` fencing supported. Below are two up‑to‑date, representative end‑to‑end flows derived from the test suite.

## 1) Core Pipeline: Tags, Capabilities, and Explicit Overrides

This mirrors the Halo flow in tests and demonstrates tag emission, capability matching, and explicit `to:*` override.

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;

// Simple document passed between blocks
public sealed class Doc { public string Text { get; init; } = string.Empty; }

// Emit tags based on content
public sealed class ParseAndTag : IMagikBlock<string, Doc>
{
    public Task<Doc> DoMagik(string input)
    {
        if (input.Contains('!')) Tag.Add("exclaim");
        Tag.Add("style:loud");
        return Task.FromResult(new Doc { Text = input });
    }
}

// Capable when 'exclaim' is present
public sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "exclaim" };
    public Task<Doc> DoMagik(Doc input)
        => Task.FromResult(new Doc { Text = $"☀ PRAISE THE SUN! ☀ {input.Text} — If only I could be so grossly incandescent." });
}

public sealed class UppercaseText : IMagikBlock<Doc, Doc>
{
    public Task<Doc> DoMagik(Doc input) => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
}

public sealed class LowercaseText : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    public IReadOnlyCollection<string> SupportedTags => new[] { "style:quiet" };
    public Task<Doc> DoMagik(Doc input) => Task.FromResult(new Doc { Text = input.Text.ToLowerInvariant() });
}

public sealed class ToOut : IMagikBlock<Doc, string>
{
    public Task<string> DoMagik(Doc input) => Task.FromResult(input.Text);
}

// Build: string -> string (push mode)
var coven = new MagikBuilder<string, string>()
    .MagikBlock(new ParseAndTag())                                 // emits: exclaim, style:loud
    .MagikBlock<Doc, Doc>(new AddSalutation())
    .MagikBlock<Doc, Doc>(new UppercaseText(), new[] { "style:loud" }) // builder-assigned capability
    .MagikBlock<Doc, string>(new ToOut())                           // stop before LowercaseText
    .MagikBlock<Doc, Doc>(new LowercaseText())                      // would apply on 'style:quiet'
    .Done();

// Default route (capability + order)
var output1 = await coven.Ritual<string, string>("hello coven!!!");

// Explicit override with 'to:*' fencing to force Lowercase path
var output2 = await coven.Ritual<string, string>(
    "Hello Coven!!!",
    new List<string> { "to:AddSalutation", "to:LowercaseText" });
```

Notes
- Runtime selection: explicit `to:*` > capability overlap > registration order.
- Tags are per‑invocation; use `Tag.Add(...)`/`Tag.Contains(...)` inside blocks only.
- `Done()` precompiles pipelines for consistent performance.

## 2) Spellcasting + Agents via DI

An end‑to‑end that composes a `MagikUser` which prepares books and invokes an agent; composed via DI.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;
using Coven.Core.Builder;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Validation;

public sealed record ChangeRequest(string RepoRoot, string Goal);
public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

// Our custom Guide carries SpellContext while still deriving from DefaultGuide
public sealed record GuideWithContext(string Markdown, SpellContext Context) : DefaultGuide(Markdown);

public sealed class AgentUserWithGuideContext : MagikUser<ChangeRequest, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    private readonly IAgentValidation _validator;

    public AgentUserWithGuideContext(ICovenAgent<FixSpell, string> agent, IAgentValidation validator)
        : base(
            // guide: create GuideWithContext from input
            (input, ct) => new GuideWithContext(
                "# Guidebook\nFollow user intent; be safe and concise.",
                new SpellContext
                {
                    ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
                    Permissions = AgentPermissions.AutoEdit()
                }),
            // spell: keep default
            null,
            // test: keep default
            null)
    { _agent = agent; _validator = validator; }

    protected override async Task<string> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        // Downcast to access the context we packed in the guide
        var g = guide.Payload as GuideWithContext
            ?? new GuideWithContext(guide.Payload.Markdown, new SpellContext());

        // Validate Codex readiness using guide-carried context
        await _validator.ValidateAsync(g.Context, ct);

        var payload = new FixSpell(
            g.Markdown,
            spell.Payload.Version,
            test.Payload.Suite,
            input.Goal);
        return await _agent.CastSpellAsync(payload, g.Context, ct);
    }
}

// DI composition: register agent + validator and compose the user block
var services = new ServiceCollection();

services.AddSingleton<ICovenAgent<FixSpell, string>>(sp =>
{
    string ToPrompt(FixSpell f) => $"goal={f.Goal}; version={f.SpellVersion}; suite={f.TestSuite}";
    string Parse(string s) => s.Trim();
    return new CodexCliAgent<FixSpell, string>(ToPrompt, Parse);
});
services.AddSingleton<IAgentValidation>(sp => new CodexCliValidation());

services.BuildCoven(c =>
{
    c.AddBlock<ChangeRequest, string, AgentUserWithGuideContext>();
    c.Done();
});

using var sp = services.BuildServiceProvider();
var coven = sp.GetRequiredService<ICoven>();

var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
Directory.CreateDirectory(temp);
var result = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(temp, "demo-goal"));
```
