# Coven

**What it is.** A .NET engine for orchestrating multiple AI coding agents (“MagikBlocks”) that collaborate in a shared context, with routing decided at runtime by tags/capabilities. It builds on Codex CLI’s sandboxing and tooling and currently targets single‑machine use.

## Core ideas

**MagikBlock**: typed unit of work; compose into graphs with **MagikBuilder**; configuration becomes immutable at `.Done()`.
**Tagging & capabilities** drive selection. Routes to the node with the most matching tags, uses registration order as tie breaker.
**Board**: runtime that posts/consumes work; supports Push (recommended) and Pull modes with timeout/retry control.

## Quick Start (DI + Spellcasting)

Minimal end-to-end using dependency injection and a single MagikUser. The guide carries a SpellContext so the agent operates relative to a repo path.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Di;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;

// Inputs + payload
public sealed record ChangeRequest(string RepoRoot, string Goal);
public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

// Guide carries SpellContext (repo root + permissions)
public sealed record GuideWithContext(string Markdown, SpellContext Context) : DefaultGuide(Markdown);

// Single user that prepares books and invokes the agent
public sealed class SpellUser : MagikUser<ChangeRequest, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    public SpellUser(ICovenAgent<FixSpell, string> agent)
        : base(
            (input, ct) => new GuideWithContext(
                "# Guidebook\nFollow user intent; be safe and concise.",
                new SpellContext
                {
                    ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
                    Permissions = AgentPermissions.AutoEdit()
                }),
            null, // default spell (0.1)
            null) // default tests (smoke)
    { _agent = agent; }

    protected override Task<string> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        var g = (GuideWithContext)guide.Payload;
        var payload = new FixSpell(g.Markdown, spell.Payload.Version, test.Payload.Suite, input.Goal);
        return _agent.CastSpellAsync(payload, g.Context, ct);
    }
}

// DI wiring: register Codex CLI agent, compose pipeline, and run
var services = new ServiceCollection();
services.AddSingleton<ICovenAgent<FixSpell, string>>(sp =>
{
    string ToPrompt(FixSpell f) => $"goal={f.Goal}; version={f.SpellVersion}; suite={f.TestSuite}";
    string Parse(string s) => s.Trim();
    return new CodexCliAgent<FixSpell, string>(ToPrompt, Parse);
});
services.BuildCoven(c => { c.AddBlock<ChangeRequest, string, SpellUser>(); c.Done(); });

using var sp = services.BuildServiceProvider();
var coven = sp.GetRequiredService<ICoven>();

var repo = Path.Combine(Path.GetTempPath(), "coven-demo");
Directory.CreateDirectory(repo);
var output = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(repo, "demo-goal"));
```

Note: Ensure `codex` is on your PATH, or pass `new CodexCliAgent<FixSpell, string>.Options { ExecutablePath = "/absolute/path/to/codex" }` when constructing the agent.

See fuller, two-part end‑to‑end examples in `/Architecture/EndToEndExample.md`.

## Samples

Explore runnable examples in `/samples`. Open `samples/Coven.Samples.sln` to browse all samples side-by-side, or use each sample’s individual `.sln` in its folder.

# Appendix 

- [Code Index](/INDEX.md)
- [Architecture Guide](/Architecture/README.md)
- [Spellcasting (Design)](/Architecture/Spellcasting/Spellcasting.md)

**License.** Dual‑license: MIT for individuals, nonprofits, and orgs under USD \$100M revenue; commercial license required at ≥ \$100M. Contact: Autumn Wyborny.

- [Notice](/NOTICE)
- [License](/LICENSE)
- [Commercial Licensing](/LICENSE-COMMERCIAL.md)

[![Support on Patreon](https://img.shields.io/badge/Support-Patreon-e85b46?logo=patreon)](https://www.patreon.com/c/Goldenwitch)
