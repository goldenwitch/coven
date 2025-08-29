# COVEN.Spellcasting — Design (single‑file, no composites)

> Provide three canonical “books” (Guide/Spell/Test) to agent code automatically, keeping the system **unopinionated**, **code‑first**, and **type‑safe**. Agents remain user‑owned; no external config files or orchestration in this layer. This doc reflects the simplified design **without composite factories** and is intended to live as a single Markdown file alongside the library source.

---

## Goals

- Treat **MagikUser** as a first‑class `IMagikBlock<TIn,TOut>` that users inherit to write their own agent logic.
- Automatically build and pass **three canonical books** into `InvokeAsync`:
  - **Guidebook<TGuide>** — usually Markdown guidance, but fully generic.
  - **Spellbook<TSpell>** — typed structure describing recipes/instructions.
  - **Testbook<TTest>** — typed structure describing scenarios/invariants.
- Keep the **public API tiny** and focused on what developers implement.
- Keep **factories optional**. Typical developers get defaults; advanced teams can inject their own factories and typed payloads.
- **No composite factories.** Each book has a single, focused factory. Parallelization happens in the base class.

---

## Public Surface (library)

The types below constitute the public surface users consume (unless noted as `internal`).

```csharp
namespace Coven.Spellcasting;

using System;
using System.Threading;
using System.Threading.Tasks;

// 0) Covariant book abstraction so derived payloads can flow through DI cleanly
public interface IBook<out T>
{
    T Payload { get; }
}

// 1) Canonical book shapes
public sealed record Guidebook<T>(T Payload) : IBook<T>;
public sealed record Spellbook<T>(T Payload) : IBook<T>;
public sealed record Testbook<T>(T Payload)  : IBook<T>;

// 2) Factories – advanced usage (typical users rely on defaults)
public interface IGuidebookFactory<TIn, TGuide>
{
    Task<IBook<TGuide>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ISpellbookFactory<TIn, TSpell>
{
    Task<IBook<TSpell>> CreateAsync(TIn input, CancellationToken ct);
}

public interface ITestbookFactory<TIn, TTest>
{
    Task<IBook<TTest>> CreateAsync(TIn input, CancellationToken ct);
}

// 3) Base block – developers only implement InvokeAsync
public abstract class MagikUser<TIn, TOut, TGuide, TSpell, TTest> : IMagikBlock<TIn, TOut>
{
    private readonly IGuidebookFactory<TIn, TGuide> _guideFactory;
    private readonly ISpellbookFactory<TIn, TSpell> _spellFactory;
    private readonly ITestbookFactory<TIn, TTest>   _testFactory;

    protected MagikUser(
        IGuidebookFactory<TIn, TGuide> guideFactory,
        ISpellbookFactory<TIn, TSpell> spellFactory,
        ITestbookFactory<TIn, TTest>   testFactory)
    {
        _guideFactory = guideFactory ?? throw new ArgumentNullException(nameof(guideFactory));
        _spellFactory = spellFactory ?? throw new ArgumentNullException(nameof(spellFactory));
        _testFactory  = testFactory  ?? throw new ArgumentNullException(nameof(testFactory));
    }

    // Coven calls DoMagik(TIn). Keep a CT-aware helper too.
    public Task<TOut> DoMagik(TIn input) => RunAsync(input, CancellationToken.None);

    public async Task<TOut> RunAsync(TIn input, CancellationToken ct)
    {
        // Build books in parallel to minimize wall time if factories touch I/O.
        var gTask = _guideFactory.CreateAsync(input, ct);
        var sTask = _spellFactory.CreateAsync(input, ct);
        var tTask = _testFactory .CreateAsync(input, ct);

        await Task.WhenAll(gTask, sTask, tTask).ConfigureAwait(false);

        var guide = await gTask.ConfigureAwait(false);
        var spell = await sTask.ConfigureAwait(false);
        var test  = await tTask.ConfigureAwait(false);

        return await InvokeAsync(input, guide, spell, test, ct).ConfigureAwait(false);
    }

    protected abstract Task<TOut> InvokeAsync(
        TIn input,
        IBook<TGuide> guidebook,
        IBook<TSpell> spellbook,
        IBook<TTest>  testbook,
        CancellationToken ct);
}

// 4) “Just‑works” defaults (internal factories + 2‑arity convenience base)
public record DefaultGuide(string Markdown =
    "# Guidebook\\nFollow user intent; be safe and concise."
);

public record DefaultSpell(string Version = "0.1");
public record DefaultTest(string Suite = "smoke");

internal sealed class DefaultGuideFactory<TIn> : IGuidebookFactory<TIn, DefaultGuide>
{
    public Task<IBook<DefaultGuide>> CreateAsync(TIn input, CancellationToken ct) =>
        Task.FromResult<IBook<DefaultGuide>>(new Guidebook<DefaultGuide>(new DefaultGuide()));
}

internal sealed class DefaultSpellFactory<TIn> : ISpellbookFactory<TIn, DefaultSpell>
{
    public Task<IBook<DefaultSpell>> CreateAsync(TIn input, CancellationToken ct) =>
        Task.FromResult<IBook<DefaultSpell>>(new Spellbook<DefaultSpell>(new DefaultSpell()));
}

internal sealed class DefaultTestFactory<TIn> : ITestbookFactory<TIn, DefaultTest>
{
    public Task<IBook<DefaultTest>> CreateAsync(TIn input, CancellationToken ct) =>
        Task.FromResult<IBook<DefaultTest>>(new Testbook<DefaultTest>(new DefaultTest()));
}

public abstract class MagikUser<TIn, TOut>
  : MagikUser<TIn, TOut, DefaultGuide, DefaultSpell, DefaultTest>
{
    protected MagikUser()
      : base(new DefaultGuideFactory<TIn>(),
             new DefaultSpellFactory<TIn>(),
             new DefaultTestFactory<TIn>()) { }

    protected MagikUser(
        IGuidebookFactory<TIn, DefaultGuide> guideFactory,
        ISpellbookFactory<TIn, DefaultSpell> spellFactory,
        ITestbookFactory<TIn, DefaultTest>   testFactory)
      : base(guideFactory, spellFactory, testFactory) { }
}
```

---

## DI Defaults Builder (library)

The Spellcasting library ships DI helpers to configure default books via delegates, without exposing factory types.

```csharp
namespace Coven.Spellcasting.Di;

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

public static class SpellcastingServiceCollectionExtensions
{
    public static IServiceCollection AddSpellcastingDefaults<TIn>(
        this IServiceCollection services,
        Action<DefaultBooksBuilder<TIn>> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var b = new DefaultBooksBuilder<TIn>();
        configure(b);
        b.Apply(services);
        return services;
    }
}

public sealed class DefaultBooksBuilder<TIn>
{
    private Func<TIn, CancellationToken, DefaultGuide>? makeGuide;
    private Func<TIn, CancellationToken, DefaultSpell>? makeSpell;
    private Func<TIn, CancellationToken, DefaultTest>?  makeTest;

    public DefaultBooksBuilder<TIn> UseGuide(Func<TIn, CancellationToken, DefaultGuide> make)
    { makeGuide = make; return this; }

    public DefaultBooksBuilder<TIn> UseSpell(Func<TIn, CancellationToken, DefaultSpell> make)
    { makeSpell = make; return this; }

    public DefaultBooksBuilder<TIn> UseTest(Func<TIn, CancellationToken, DefaultTest> make)
    { makeTest = make; return this; }

    internal void Apply(IServiceCollection services)
    {
        if (makeGuide is not null)
            services.AddSingleton<IGuidebookFactory<TIn, DefaultGuide>>(sp =>
                new DelegateGuideFactory<TIn>(makeGuide));
        if (makeSpell is not null)
            services.AddSingleton<ISpellbookFactory<TIn, DefaultSpell>>(sp =>
                new DelegateSpellFactory<TIn>(makeSpell));
        if (makeTest is not null)
            services.AddSingleton<ITestbookFactory<TIn, DefaultTest>>(sp =>
                new DelegateTestFactory<TIn>(makeTest));
    }
}
```

Notes
- The library depends on `Microsoft.Extensions.DependencyInjection.Abstractions` for these helpers.
- Users who prefer full control can continue to register their own `I*Factory` implementations.

---

## Usage

### 1) Zero‑setup (typical)

```csharp
public sealed record ChangeRequest(string Goal, string RepoRoot);
public sealed record PatchPlan(string Summary);

public sealed class MyUser : Coven.Spellcasting.MagikUser<ChangeRequest, PatchPlan>
{
    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        IBook<Coven.Spellcasting.DefaultGuide> guide,
        IBook<Coven.Spellcasting.DefaultSpell> spell,
        IBook<Coven.Spellcasting.DefaultTest>  test,
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

### 2) Advanced typed books (no composites, derived-friendly)

```csharp
public sealed record MyGuide(string Markdown, string Role) : Coven.Spellcasting.DefaultGuide(Markdown);
public sealed record MySpellV1(string Version, System.Collections.Generic.IReadOnlyList<object> Steps);
public sealed record MyTestsV1(string Version, System.Collections.Generic.IReadOnlyList<string> Cases);

public sealed class MyGuideFactory : Coven.Spellcasting.IGuidebookFactory<ChangeRequest, Coven.Spellcasting.DefaultGuide>
{
    public Task<Coven.Spellcasting.IBook<Coven.Spellcasting.DefaultGuide>> CreateAsync(ChangeRequest input, CancellationToken ct) =>
        Task.FromResult<Coven.Spellcasting.IBook<Coven.Spellcasting.DefaultGuide>>(
            new Coven.Spellcasting.Guidebook<Coven.Spellcasting.DefaultGuide>(
                new MyGuide("# Guidebook\nYou are Coven.", "Senior Assistant")));
}

public sealed class MySpellFactory : Coven.Spellcasting.ISpellbookFactory<ChangeRequest, MySpellV1>
{
    public Task<Coven.Spellcasting.IBook<MySpellV1>> CreateAsync(ChangeRequest input, CancellationToken ct) =>
        Task.FromResult<Coven.Spellcasting.IBook<MySpellV1>>(
            new Coven.Spellcasting.Spellbook<MySpellV1>(new MySpellV1("0.1", Array.Empty<object>())));
}

public sealed class MyTestFactory : Coven.Spellcasting.ITestbookFactory<ChangeRequest, MyTestsV1>
{
    public Task<Coven.Spellcasting.IBook<MyTestsV1>> CreateAsync(ChangeRequest input, CancellationToken ct) =>
        Task.FromResult<Coven.Spellcasting.IBook<MyTestsV1>>(
            new Coven.Spellcasting.Testbook<MyTestsV1>(new MyTestsV1("0.1", new[] { "rename_method_happy" })));
}

public sealed class MyTypedUser
  : Coven.Spellcasting.MagikUser<ChangeRequest, PatchPlan, Coven.Spellcasting.DefaultGuide, MySpellV1, MyTestsV1>
{
    public MyTypedUser()
      : base(new MyGuideFactory(), new MySpellFactory(), new MyTestFactory()) { }

    protected override Task<PatchPlan> InvokeAsync(
        ChangeRequest input,
        Coven.Spellcasting.IBook<Coven.Spellcasting.DefaultGuide> guide, // actually Guidebook<MyGuide> under the hood
        Coven.Spellcasting.IBook<MySpellV1>    spell,
        Coven.Spellcasting.IBook<MyTestsV1>    test,
        CancellationToken ct)
    {
        return Task.FromResult(new PatchPlan("typed ok"));
    }
}
```

---

## Design Notes & Trade‑offs

- **No composite factories:** Simpler surface and clearer ownership. If teams need to share expensive prep, they can do so inside their *individual* factories or by introducing their own aggregator types outside this layer.
- **Concurrency:** The base `RunAsync` builds the three books in parallel via `Task.WhenAll`. This is purely an implementation detail; the API remains minimal.
- **Covariant `IBook<T>`:** Allows returning derived payloads while exposing the base type to consumers—handy with DI and inheritance of defaults.
- **Defaults are internal:** Default factories are `internal` to keep “zero‑setup” convenient without encouraging hard dependencies.
- **Alignment with Coven:** `DoMagik(TIn)` satisfies `IMagikBlock<TIn,TOut>`; `RunAsync` provides a CT-aware path for callers outside Coven’s pipeline.

---

## Lifecycle & Guarantees

- **Construction:** Factories are captured in the constructor. No config files are read by this layer.
- **Execution:** `DoMagik` (or `RunAsync`) constructs Guide/Spell/Test then calls `InvokeAsync` with all three plus the original input.
- **Ownership:** This library does not define “the agent.” It is agnostic to transport (CLI/HTTP/RPC) and model family.
- **Typing:** Payloads are fully generic; teams can evolve schemas without changing the core surface.

---

## Non‑Goals (MVP)

- No opinionated context gathering, tool execution, guardrails, or test running in this layer.
- No dependency on YAML/JSON configuration files.
- No runtime policy engine; this is a thin composition helper for agent code.
