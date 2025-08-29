
# Coven.Spellcasting.Agents

> Scope. Provide a tiny, future‑proof agent abstraction that fits Coven’s generic patterns (`TIn, TOut`), keeps serialization out of the public API, uses type‑safe permissions, and composes naturally with `Coven.Spellcasting.MagikUser<TIn,TOut>`. Examples use a MagikUser that calls the agent inside `InvokeAsync`.

---

## Principles (DX first)

- **Minimal public surface.** Only expose what consuming developers must call or extend. Everything else is `internal` or `sealed`.
- **Arbitrary symbols in/out.** Agents are generic `TIn, TOut` and do not prescribe serialization.
- **Type‑safe capabilities.** Replace stringly “allow gates” with typed *action symbols* developers can extend without breaking changes.
- **MagikUser‑first.** Agent logic is invoked via **`Invoke`** on a `MagikUser`, which is a first‑class `MagikBlock` in Coven’s model.
- **Ritual composition.** All examples build a ritual with `MagikBuilder` and run via `Ritual<TIn, TOut>(…)`.

> The README shows Coven’s `MagikBlock`, `MagikBuilder`, and `Ritual` flow. We align to that here.¹

---

## Public API (what we expose)

> Everything below is the **only** public surface of `Coven.Spellcasting.Agents`. Implementation helpers & DI plumping stay internal unless noted.

```csharp
namespace Coven.Spellcasting.Agents;

public interface ICovenAgent<TIn, TOut>
{
    string Id { get; }  // e.g., "codex"

    Task<TOut> CastSpellAsync(
        TIn input,
        SpellContext? context = null,
        CancellationToken ct = default);
}

// Marker for addon context units (“facets”)
public interface ISpellContextFacet { }

// Keep this sealed to preserve invariants and versioning freedom.
public sealed record SpellContext
{
    public Uri? ContextUri { get; init; }
    public AgentPermissions? Permissions { get; init; }

    // Internal bag of typed facets (not directly mutable by consumers).
    public IReadOnlyDictionary<Type, object> Facets { get; private init; }
        = new Dictionary<Type, object>();

    // Add or replace a facet (returns a new SpellContext).
    public SpellContext With<TFacet>(TFacet facet) where TFacet : class, ISpellContextFacet
    {
        var dict = new Dictionary<Type, object>(Facets) { [typeof(TFacet)] = facet };
        return this with { Facets = dict };
    }

    // Typed retrieval (null if absent).
    public TFacet? Get<TFacet>() where TFacet : class, ISpellContextFacet
        => Facets.TryGetValue(typeof(TFacet), out var o) ? (TFacet)o : null;

    // Optional convenience if you like TryGet patterns:
    public bool TryGet<TFacet>(out TFacet? facet) where TFacet : class, ISpellContextFacet
    {
        facet = Get<TFacet>();
        return facet is not null;
    }
}


// Marker types = capability “symbols” (extensible without breaking changes).
public interface ISpellAction { }

public sealed class WriteFile     : ISpellAction { }
public sealed class RunCommand    : ISpellAction { }
public sealed class NetworkAccess : ISpellAction { }

public sealed class AgentPermissions
{
    private readonly HashSet<Type> _grants = new();

    public AgentPermissions Grant<TAction>() where TAction : ISpellAction
    { _grants.Add(typeof(TAction)); return this; }

    public bool Allows<TAction>() where TAction : ISpellAction
        => _grants.Contains(typeof(TAction));

    // Convenience presets (tiny & opinionated)
    public static AgentPermissions None()        => new();
    public static AgentPermissions AutoEdit()    => new AgentPermissions().Grant<WriteFile>();
    public static AgentPermissions FullAuto()    => new AgentPermissions().Grant<WriteFile>().Grant<RunCommand>();
}
```

**Why these are `public`:**
- Consumers **must** reference `ICovenAgent<TIn,TOut>`.
- Many agents need to accept a **context** and query **permissions**.
- Consumers may mint their own action symbols by implementing `ISpellAction`.

Everything else (e.g., defaults, process adapters, utilities) is `internal` and sealed unless consumption requires otherwise.

---

## How MagikUser participates

> Contract. Implement `Coven.Spellcasting.MagikUser<TIn,TOut>`. Inside `InvokeAsync`, prepare a typed payload from the books, construct `SpellContext` from input as needed, call the agent via `ICovenAgent<TIn,TOut>.CastSpellAsync`, and return the result.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;

public sealed record ChangeRequest(string RepoRoot, string Goal);
public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

public sealed class EndToEndUser : MagikUser<ChangeRequest, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    public EndToEndUser(ICovenAgent<FixSpell, string> agent) => _agent = agent;

    protected override Task<string> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        var payload = new FixSpell(
            guide.Payload.Markdown,
            spell.Payload.Version,
            test.Payload.Suite,
            input.Goal);

        var ctx = new SpellContext
        {
            ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
            Permissions = AgentPermissions.AutoEdit()
        };

        return _agent.CastSpellAsync(payload, ctx, ct);
    }
}
```

---

## Example concrete agent — process/CLI adapter (Codex‑style)

> This adapter is intentionally generic: it only requires `Func<TIn,string>` and `Func<string,TOut>`. It does not expose serialization in the public API.

```csharp
// Coven.Spellcasting.Agents.Codex (separate package): public because consumers will new it up.
//
// Public surface of this package is just the agent type and a small Options record.
// All helper classes remain internal.
using System.Diagnostics;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex;

public sealed class CodexCliAgent<TIn, TOut> : ICovenAgent<TIn, TOut>
{
    public sealed class Options
    {
        public string ExecutablePath { get; init; } = "codex";
        public IReadOnlyList<string> FixedArgs { get; init; } = Array.Empty<string>(); // keep simple
    }

    public string Id => "codex";

    private readonly Func<TIn, string>  _toPrompt;
    private readonly Func<string, TOut> _parse;
    private readonly Options _opts;

    public CodexCliAgent(Func<TIn, string> toPrompt, Func<string, TOut> parse, Options? options = null)
    { _toPrompt = toPrompt ?? throw new ArgumentNullException(nameof(toPrompt));
      _parse    = parse    ?? throw new ArgumentNullException(nameof(parse));
      _opts     = options  ?? new Options(); }

    public async Task<TOut> CastSpellAsync(
        TIn input,
        SpellContext? context = null,
        CancellationToken ct = default)
    {
        var prompt = _toPrompt(input);

        // Map type‑safe permissions to a simple autonomy signal if needed.
        var perms = context?.Permissions;
        var autonomy = perms?.Allows<RunCommand>() == true ? "full-auto"
                     :  perms?.Allows<WriteFile>()  == true ? "auto-edit"
                     :  "suggest";

        var psi = new ProcessStartInfo
        {
            FileName = _opts.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var a in _opts.FixedArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(prompt); // keep the adapter unopinionated about flags

        // Optional: derive a working dir from file:// URIs only.
        if (context?.ContextUri is { IsAbsoluteUri: true, Scheme: "file" } uri)
            psi.WorkingDirectory = Path.GetFullPath(uri.LocalPath);

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        var text = stdout.Length > 0 ? stdout : stderr;
        return _parse(text);
    }
}
```

> **Publicness check.** `CodexCliAgent<,>` and its `Options` are **public sealed** because app code constructs and configures them. All other implementation details are internal.

---

## Ritual example

```csharp
using Coven.Core.Builder;
using Coven.Spellcasting;
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Codex;

public sealed record ChangeRequest(string RepoRoot, string Goal);
public sealed record FixSpell(string GuideMarkdown, string SpellVersion, string TestSuite, string Goal);

public sealed class EndToEndUser : MagikUser<ChangeRequest, string>
{
    private readonly ICovenAgent<FixSpell, string> _agent;
    public EndToEndUser(ICovenAgent<FixSpell, string> agent) => _agent = agent;

    protected override Task<string> InvokeAsync(
        ChangeRequest input,
        IBook<DefaultGuide> guide,
        IBook<DefaultSpell> spell,
        IBook<DefaultTest>  test,
        CancellationToken ct)
    {
        var payload = new FixSpell(guide.Payload.Markdown, spell.Payload.Version, test.Payload.Suite, input.Goal);
        var ctx = new SpellContext
        {
            ContextUri = new Uri($"file://{Path.GetFullPath(input.RepoRoot)}"),
            Permissions = AgentPermissions.AutoEdit()
        };
        return _agent.CastSpellAsync(payload, ctx, ct);
    }
}

var agent = new CodexCliAgent<FixSpell, string>(
    toPrompt: s => $"Guide:\n{s.GuideMarkdown}\n\nGoal:\n{s.Goal}",
    parse: text => text);

var coven = new MagikBuilder<ChangeRequest, string>()
    .MagikBlock<ChangeRequest, string>(new EndToEndUser(agent))
    .Done();

var result = await coven.Ritual<ChangeRequest, string>(new ChangeRequest(repoRoot: "/src/project", Goal: "implement feature X"));
Console.WriteLine(result);
```

---

## Extending SpellContext

```csharp

public sealed class GitFacet : ISpellContextFacet
{
    public string RepoRoot { get; }
    public string? Branch { get; }
    public string? Commit { get; }
    public GitFacet(string repoRoot, string? branch = null, string? commit = null)
        => (RepoRoot, Branch, Commit) = (repoRoot, branch, commit);
}

// Build a context with an extra facet:
var ctx = new SpellContext
{
    ContextUri = new Uri("file:///src/project"),
    Permissions = AgentPermissions.AutoEdit()
}.With(new GitFacet(repoRoot: "/src/project", branch: "main"));

```

## Public vs. internal — quick grid

| Component | Public? | Why |
|---|---|---|
| `ICovenAgent<TIn,TOut>` | Yes | Primary extension point for writing/plugging agents |
| `SpellContext` | Yes | Callers provide context; agents read it |
| `ISpellAction` | Yes | Callers can mint custom action symbols |
| `WriteFile`, `RunCommand`, `NetworkAccess` | Yes (sealed) | Common symbols users will reference |
| `AgentPermissions` | Yes (sealed) | Users build permission sets |
| `CodexCliAgent<,>` & `Options` | Yes (sealed) | Constructed/configured by app code |

Everything else remains **internal** by default. Make a symbol public only when you expect external code to **call** it or **extend** it.

---

## Intentionally omitted (by design)

- **Serialization knobs** in the public API — let each agent implementation or caller decide.
- **Streaming/events** — add later when a concrete need appears.
- **Metadata/metrics/session APIs** — keep out of the core until stable across agents.
- **Stringly permissions** — replaced with typed symbols to avoid breakage and typos.

---
