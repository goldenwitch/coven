# Coven

**What it is.** A .NET engine for orchestrating multiple AI coding agents (“MagikBlocks”) that collaborate in a shared context, with routing decided at runtime by tags/capabilities. It builds on Codex CLI’s sandboxing and tooling and currently targets single‑machine use.

**Core ideas.**

* **MagikBlock**: typed unit of work; compose into graphs with **MagikBuilder**; configuration becomes immutable at `.Done()`.
* **Tagging & capabilities** drive selection. Routes to the node with the most matching tags, uses registration order as tie breaker.
* **Board**: runtime that posts/consumes work; supports Push (recommended) and Pull modes with timeout/retry control.

# Quick start
Magikblocks scale from tiny applications like below to sprawling multi-agent workflows.

## Build a pipeline and run it

Here’s a minimal ritual that takes a string, reverses the words, and rejoins them.

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;

public sealed class SplitWords : IMagikBlock<string, string[]>
{
    public Task<string[]> DoMagik(string input) =>
        Task.FromResult(input.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed class ReverseWords : IMagikBlock<string[], string[]>
{
    public Task<string[]> DoMagik(string[] words) =>
        Task.FromResult(words.Reverse().ToArray());
}

public sealed class JoinWords : IMagikBlock<string[], string>
{
    public Task<string> DoMagik(string[] words) =>
        Task.FromResult(string.Join(" ", words) + " 🌞");
}

public class App
{
    public static async Task Main()
    {
        var coven = new MagikBuilder<string, string>()
            .MagikBlock(new SplitWords())
            .MagikBlock(new ReverseWords())
            .MagikBlock(new JoinWords())
            .Done();

        var input = "incandescent grossly so be could I only If";
        var result = await coven.Ritual<string, string>(input);

        Console.WriteLine(result);
        // Output: If only I could be so grossly incandescent 🌞
    }
}
```

# Spellcasting

Spellcasting makes agent blocks simple and type‑safe by providing three canonical “books” to your code: a `Guidebook<TGuide>`, a `Spellbook<TSpell>`, and a `Testbook<TTest>`. Inherit from `Coven.Spellcasting.MagikUser<TIn,TOut>` and implement a single `InvokeAsync` method; the books are created for you (with sensible defaults) and passed in.

Quick example using defaults:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Coven.Core.Builder;
using Coven.Spellcasting;

public sealed record ChangeRequest(string Goal);
public sealed record PatchPlan(string GuideMarkdown);

public sealed class MyUser : MagikUser<ChangeRequest, PatchPlan>
{
    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct) =>
        Task.FromResult(new PatchPlan(guide.Payload.Markdown));
}

var coven = new MagikBuilder<ChangeRequest, PatchPlan>()
    .MagikBlock<ChangeRequest, PatchPlan>(new MyUser())
    .Done();
var result = await coven.Ritual<ChangeRequest, PatchPlan>(new("demo"));
```

- Configure defaults via DI: `services.AddSpellcastingDefaults<TIn>(b => b.UseGuide(...).UseSpell(...).UseTest(...));`
- For typed books, inherit `MagikUser<TIn,TOut,TGuide,TSpell,TTest>` and inject your factories.
- Design doc: see Arch/Spellcasting/Spellcasting.md for goals, API, and patterns.

# Appendix 


- **Detailed Architecture.** [Click here](/Arch/README.md)
- **Coven.Spellcasting** [Click here](/Arch/Spellcasting/Spellcasting.md)

**License.** Dual‑license: MIT for individuals, nonprofits, and orgs under USD \$100M revenue; commercial license required at ≥ \$100M. Contact: Autumn Wyborny.

- [Notice](/NOTICE)
- [License](/LICENSE)
- [Commercial Licensing](/LICENSE-COMMERCIAL.md)

[![Support on Patreon](https://img.shields.io/badge/Support-Patreon-e85b46?logo=patreon)](https://www.patreon.com/c/Goldenwitch)
