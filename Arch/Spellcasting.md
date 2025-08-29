# COVEN.SPELLCASTING.md

> **COVEN Spellcasting**  
> Purpose: provide three canonical “books” to agent code automatically, while keeping the system **unopinionated**, **code‑first**, and **type‑safe**.  
> Agents are user‑owned. No external config files. No runtime orchestration logic in this layer.

---

## Goals

- Treat **MagikUser** as a first‑class `IMagikBlock<TIn,TOut>` that users inherit to write their own agent logic.
- Automatically build and pass **three canonical books** into `InvokeAsync`:
  - **Guidebook<TGuide>** – usually Markdown, but fully generic.
  - **Spellbook<TSpell>** – typed structure describing recipes/instructions.
  - **Testbook<TTest>** – typed structure describing scenarios/invariants.
- Keep the **public API tiny** and focused on what developers implement.
- Make **factories optional**. Typical developers get defaults; advanced teams can inject their own factories and typed payloads.

---

## Public Surface

```csharp
namespace Coven.Spellcasting;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


// 1) Canonical, generic book shapes (no JSON requirement)
public sealed record Guidebook<TGuide>(TGuide Payload);

public sealed record Spellbook<TSpell>(TSpell Payload);

public sealed record Testbook<TTest>(TTest Payload);

// 2) Factories – advanced usage (typical users rely on defaults)
public interface IGuidebookFactory<TIn, TGuide>
{
    Task<Guidebook<TGuide>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ISpellbookFactory<TIn, TSpell>
{
    Task<Spellbook<TSpell>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ITestbookFactory<TIn, TTest>
{
    Task<Testbook<TTest>> CreateAsync(TIn input, CancellationToken ct);
}

// 3) Base block – developers only implement InvokeAsync
public abstract class MagikUser<TIn, TOut, TGuide, TSpell, TTest> : IMagikBlock<TIn, TOut>
{
    private readonly IGuidebookFactory<TIn, TGuide> _guideFactory;
    private readonly ISpellbookFactory<TIn, TSpell> _spellFactory;
    private readonly ITestbookFactory<TIn, TTest> _testFactory;

    protected MagikUser(
        IGuidebookFactory<TIn, TGuide> guideFactory,
        ISpellbookFactory<TIn, TSpell> spellFactory,
        ITestbookFactory<TIn, TTest> testFactory)
    {
        _guideFactory = guideFactory ?? throw new ArgumentNullException(nameof(guideFactory));
        _spellFactory = spellFactory ?? throw new ArgumentNullException(nameof(spellFactory));
        _testFactory  = testFactory  ?? throw new ArgumentNullException(nameof(testFactory));
    }

    public async Task<TOut> RunAsync(TIn input, CancellationToken ct)
    {
        var guide = await _guideFactory.CreateAsync(input, ct).ConfigureAwait(false);
        var spell = await _spellFactory.CreateAsync(input, ct).ConfigureAwait(false);
        var test  = await _testFactory .CreateAsync(input, ct).ConfigureAwait(false);

        return await InvokeAsync(input, guide, spell, test, ct).ConfigureAwait(false);
    }

    // Developers write their agent logic here.
    protected abstract Task<TOut> InvokeAsync(
        TIn input,
        Guidebook<TGuide> guidebook,
        Spellbook<TSpell> spellbook,
        Testbook<TTest>   testbook,
        CancellationToken ct);
}
```

---

## “Just‑Works” Defaults (for typical developers)

Most users shouldn’t implement factories. Inherit the 2‑arity `MagikUser<TIn,TOut>` which wires in default factories and default payload shapes. Advanced users can still inherit the 5‑arity `MagikUser<TIn,TOut,TGuide,TSpell,TTest>` and inject typed factories via DI.

```csharp
namespace Coven.Spellcasting;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Default payloads (can be swapped by advanced users)
public sealed record DefaultGuide(string Markdown =
    "# Guidebook\nFollow user intent; be safe and concise."
);

public sealed record DefaultSpell(string Version = "0.1");

public sealed record DefaultTest(string Suite = "smoke");

// Default factories (internal: provided by the library)
internal sealed class DefaultGuideFactory<TIn> : IGuidebookFactory<TIn, DefaultGuide>
{
    public Task<Guidebook<DefaultGuide>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Guidebook<DefaultGuide>(new DefaultGuide()));
}

internal sealed class DefaultSpellFactory<TIn> : ISpellbookFactory<TIn, DefaultSpell>
{
    public Task<Spellbook<DefaultSpell>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Spellbook<DefaultSpell>(new DefaultSpell()));
}

internal sealed class DefaultTestFactory<TIn> : ITestbookFactory<TIn, DefaultTest>
{
    public Task<Testbook<DefaultTest>> CreateAsync(TIn input, CancellationToken ct)
        => Task.FromResult(new Testbook<DefaultTest>(new DefaultTest()));
}

// Defaulted base for typical developers
public abstract class MagikUser<TIn, TOut>
  : MagikUser<TIn, TOut, DefaultGuide, DefaultSpell, DefaultTest>
{
    // Zero-setup constructor uses library defaults
    protected MagikUser()
      : base(new DefaultGuideFactory<TIn>(),
             new DefaultSpellFactory<TIn>(),
             new DefaultTestFactory<TIn>()) { }

    // Optional: allow DI overrides while staying on default book types
    protected MagikUser(
        IGuidebookFactory<TIn, DefaultGuide> guideFactory,
        ISpellbookFactory<TIn, DefaultSpell> spellFactory,
        ITestbookFactory<TIn, DefaultTest>   testFactory)
      : base(guideFactory, spellFactory, testFactory) { }
}
```

---

## Usage Examples

### 1) Zero‑setup (typical)

```csharp
// Domain types owned by the repo
public sealed record ChangeRequest(string Goal, string RepoRoot);
public sealed record PatchPlan(string Summary);

// Developer only implements InvokeAsync
public sealed class MyUser : Coven.Spellcasting.MagikUser<ChangeRequest, PatchPlan>
{
    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        Coven.Spellcasting.Guidebook<Coven.Spellcasting.DefaultGuide> guide,
        Coven.Spellcasting.Spellbook<Coven.Spellcasting.DefaultSpell> spell,
        Coven.Spellcasting.Testbook<Coven.Spellcasting.DefaultTest>   test,
        CancellationToken ct)
    {
        // Compose an agent payload, call your agent, and map to PatchPlan.
        var payload = new
        {
            guide = guide.Payload.Markdown,
            spell = spell.Payload,
            tests = test.Payload,
            request = input
        };

        // Agent call omitted. Return a stub for illustration.
        return Task.FromResult(new PatchPlan("ok"));
    }
}
```

### 2) Advanced typed books

```csharp
// Custom payloads
public sealed record MyGuide(string Markdown, string Role);
public sealed record MySpellV1(string Version, IReadOnlyList<object> Steps);
public sealed record MyTestsV1(string Version, IReadOnlyList<string> Cases);

// Custom factories
public sealed class MyGuideFactory : Coven.Spellcasting.IGuidebookFactory<ChangeRequest, MyGuide>
{
    public Task<Coven.Spellcasting.Guidebook<MyGuide>> CreateAsync(ChangeRequest input, CancellationToken ct)
        => Task.FromResult(new Coven.Spellcasting.Guidebook<MyGuide>(
               new MyGuide("# Guidebook\nYou are Coven.", "Senior Assistant")));
}

public sealed class MySpellFactory : Coven.Spellcasting.ISpellbookFactory<ChangeRequest, MySpellV1>
{
    public Task<Coven.Spellcasting.Spellbook<MySpellV1>> CreateAsync(ChangeRequest input, CancellationToken ct)
        => Task.FromResult(new Coven.Spellcasting.Spellbook<MySpellV1>(
               new MySpellV1("0.1", Array.Empty<object>())));
}

public sealed class MyTestFactory : Coven.Spellcasting.ITestbookFactory<ChangeRequest, MyTestsV1>
{
    public Task<Coven.Spellcasting.Testbook<MyTestsV1>> CreateAsync(ChangeRequest input, CancellationToken ct)
        => Task.FromResult(new Coven.Spellcasting.Testbook<MyTestsV1>(
               new MyTestsV1("0.1", new[] { "rename_method_happy" })));
}

// Advanced user block with typed books
public sealed class MyTypedUser
  : Coven.Spellcasting.MagikUser<ChangeRequest, PatchPlan, MyGuide, MySpellV1, MyTestsV1>
{
    public MyTypedUser()
      : base(new MyGuideFactory(), new MySpellFactory(), new MyTestFactory()) { }

    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        Coven.Spellcasting.Guidebook<MyGuide> guide,
        Coven.Spellcasting.Spellbook<MySpellV1> spell,
        Coven.Spellcasting.Testbook<MyTestsV1> test,
        CancellationToken ct)
    {
        // Use strongly-typed payloads.
        return Task.FromResult(new PatchPlan("typed ok"));
    }
}
```

---

## Lifecycle & Guarantees

- **Construction**: Factories are captured at construction; no external configuration files are read.
- **Run**: `RunAsync` (in the base) builds the three books and forwards them to `InvokeAsync`.
- **Ownership**: Agents are not defined here. The library is agnostic to transport (CLI/HTTP/RPC) and model family.
- **Typing**: Guide/Spell/Test payloads are fully generic; teams can evolve schemas without changing the core.

---

## Non‑Goals (MVP)

- No opinionated context gathering, tool execution, guardrails, or test running in this layer.
- No dependency on YAML/JSON configuration files.
