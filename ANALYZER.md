# Coven Roslyn Analyzer Pack

This document describes the design and usage of the **Coven Roslyn analyzer pack** that enforces core architectural constraints for MagikBlocks and tag usage. It ships as a NuGet package `Coven.Analyzers` (analyzers + code fixes) and an optional VSIX for IDEs.

> **Scope**
>
> This pack currently includes three diagnostics:
>
> - **COV001 — MagikBlock input must be immutable**
> - **COV002 — Selection strategies (`ISelectionStrategy`) must be stateless & deterministic**
> - **COV003 — Tag API usage must occur inside a MagikBlock’s execution scope (`DoMagik`)**

No other rules are included in this version.

---

## Why analyzers?

Coven emphasizes:
- **Immutable data** flowing through MagikBlocks.
- **Deterministic routing** via tags/capabilities, with tags scoped to a single request.
- **Purity** for selection strategies (`ISelectionStrategy`)—deterministic and no ambient state.

The analyzer pack provides immediate feedback in the IDE and CI so violations are caught early, with code fixes where safe.

---

## Package layout

```
/src
  Coven.Analyzers/
    Coven.Analyzers.csproj
    Rules/
      COV001_ImmutableInputAnalyzer.cs
      COV002_StaticTagSelectorAnalyzer.cs
      COV003_TagScopeUsageAnalyzer.cs
    CodeFixes/
      COV001_MakeInputRecordFix.cs
      COV002_MakeLambdaStaticFix.cs
    Helpers/
      SymbolHelpers.cs
      EditorConfigOptions.cs
      WellKnownTypes.cs
  Coven.Analyzers.Tests/
    COV001_ImmutableInputTests.cs
    COV002_StaticTagSelectorTests.cs
    COV003_TagScopeUsageTests.cs
.editorconfig
```

> **Namespaces:** The examples below assume types such as `Coven.Core.IMagikBlock<,>` and `Coven.Core.Tags.Tag`. Adjust symbol lookups if the actual namespaces differ.

---

## Diagnostics

### COV001 — MagikBlock input must be immutable

**Intent:** Every `IMagikBlock<TIn, TOut>` must accept an **immutable** input type for `TIn`.

**What counts as immutable:**

1. **Records (preferred)**
   - `record` (class): all properties are get‑only or `init;`.
   - `record struct`: declared as `readonly record struct`, or all fields are `readonly` and properties are get/init‑only.
2. **Well‑known immutable primitives**
   - `string`, numeric types, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`.
3. **Approved custom types**
   - Marked with `[CovenImmutable]` (attribute defined in `Coven.Core`), or listed in `.editorconfig` (see Configuration).

**When it fires:**
- `TIn` is a mutable class/struct (settable properties, non‑readonly fields), or not recognized as immutable.

**Severity (default):** `error` (configurable).

**Code fix (where safe):**
- Convert a mutable `class` to `record` and change auto‑properties from `set;` → `init;`.
- For structs, offer to convert to `readonly record struct` or mark fields `readonly`.
- If `TIn` is already immutable (e.g., `string`), no fix is offered.

**Examples**

_Not OK_
```csharp
public sealed class SearchRequest { public string Query { get; set; } } // settable

public sealed class SearchBlock : IMagikBlock<SearchRequest, SearchResult>
{
    public Task<SearchResult> DoMagik(SearchRequest input, CancellationToken ct) => ...;
}
```

_OK (after fix)_
```csharp
public sealed record SearchRequest(string Query);

public sealed class SearchBlock : IMagikBlock<SearchRequest, SearchResult>
{
    public Task<SearchResult> DoMagik(SearchRequest input, CancellationToken ct) => ...;
}
```

---

### COV002 — Selection strategies (`ISelectionStrategy`) must be stateless & deterministic

**Intent:** Route decisions should not depend on ambient or time-varying state. Implementations of `ISelectionStrategy` must be effectively stateless and rely only on their inputs (tags, capabilities, request, etc.).

**What is analyzed:**
- Any non-abstract type that implements an interface named `ISelectionStrategy` in the Coven assemblies (resolved by metadata name; namespaces may differ).

**Checks (all produce COV002):**
1) **Statefulness:** Instance fields or settable properties are present.
   - Allowed: no instance fields/properties, or only `readonly` fields and get-only/`init` properties whose types are immutable (records with get/init-only members; well-known BCL immutables; or types marked `[CovenImmutable]` or listed in `coven_immutable_types`).
2) **Non-determinism:** The strategy's select method(s) (e.g., `Select(...)`) use non-deterministic or ambient APIs, including but not limited to:
   - `DateTime.Now/UtcNow`, `Stopwatch.StartNew`, `Environment.TickCount64/TickCount`
   - `Guid.NewGuid()`, `Random` constructors/`Next*`
   - I/O or networking (`System.IO.*`, `HttpClient`, `Dns`, etc.)
   - reading/writing static mutable fields
   - thread/current-culture/Environment variables
   (Use COV003 for tag mutation detection.)
3) **Subclassing:** Strategy type is not `sealed`. (Info-level suggestion to keep behavior predictable.)

**Severity (default):** `warning` (configurable; the subclassing suggestion is `suggestion`).

**Code fix:**
- For (1): make fields `readonly`; convert `set;` → `init;` or remove setters and add a constructor; for collections, suggest `ImmutableArray<>`/`IReadOnlyList<>`.
- For (2): diagnostic-only with guidance; suggest injecting a clock/PRNG via parameters or the DI container, and thread values through the method signature.
- For (3): offer to add the `sealed` modifier.

**Examples**

_Not OK_
```csharp
public class RandomStrategy : ISelectionStrategy
{
    private readonly Random _rng = new();              // non-deterministic seed
    public string Mode { get; set; } = "fast";         // settable state

    public Selection Select(Request r, TagBag tags) =>
        _rng.Next(2) == 0 ? Selection.Left : Selection.Right; // nondeterminism
}
```

_OK_
```csharp
public sealed class DeterministicStrategy : ISelectionStrategy
{
    public Selection Select(Request r, TagBag tags)
    {
        // relies only on inputs; no ambient state
        var wantsFast = tags.Contains("want:fast");
        return wantsFast ? Selection.Left : Selection.Right;
    }
}
```
### COV003 — Tag API usage must be inside `DoMagik` execution scope

**Intent:** Tags are **scoped per request**. Reading or mutating tags should happen **only during a block’s execution**, not in constructors, field initializers, or arbitrary helpers that run outside the request context.

**What is analyzed:**
- Calls to `Tag.Add(...)`, `Tag.Contains(...)`, etc.

**Checks:**
- The call must be inside the `DoMagik` implementation (or its inlined wrapper) of a type implementing `IMagikBlock<TIn, TOut>`.

**Severity (default):** `warning` (configurable).

**Examples**

_Not OK_
```csharp
public sealed class ResolveUserBlock : IMagikBlock<UserRequest, User>
{
    public ResolveUserBlock()
    {
        Tag.Add("by:ctor"); // flagged: runs outside request scope
    }

    public Task<User> DoMagik(UserRequest input, CancellationToken ct) => ...;
}
```

_OK_
```csharp
public sealed class ResolveUserBlock : IMagikBlock<UserRequest, User>
{
    public Task<User> DoMagik(UserRequest input, CancellationToken ct)
    {
        Tag.Add("by:resolve-user");
        ...
    }
}
```

---

## Configuration

The analyzers are configurable via `.editorconfig`.

```ini
# ---- Coven analyzer defaults ----
dotnet_diagnostic.COV001.severity = error
dotnet_diagnostic.COV002.severity = warning
dotnet_diagnostic.COV003.severity = warning

# Additional immutable types for COV001 (semicolon-separated, fully-qualified)
coven_immutable_types = System.Uri;System.Version;MyCompany.Domain.ImmutableFoo
```

> **Custom attribute:** The `[CovenImmutable]` attribute (defined in `Coven.Core`) can be applied to a type to treat it as immutable for COV001. The analyzer recognizes the attribute by name; no direct reference to the analyzer package is required.

---

## Code fixes in detail

### COV001_MakeInputRecordFix
- Converts
  ```csharp
  public sealed class Foo { public string Bar { get; set; } }
  ```
  into
  ```csharp
  public sealed record Foo(string Bar);
  ```
  or, when preserving members is required:
  ```csharp
  public sealed record Foo
  {
      public string Bar { get; init; }
      // preserves other members, annotations, and nullability
  }
  ```
- For structs, offers `readonly record struct Foo(...)` where applicable.

> The fix avoids changing public APIs across assemblies unless the symbol is internal. For public types, a preview diff is shown and the fix is gated with a confirmation dialog (IDE experience).

### COV002_StatelessStrategyFix
- Adds `sealed` to the strategy class.
- Converts settable properties to get-only/`init` and adds a constructor as needed.
- Marks fields `readonly`; offers to change common mutable collections to `ImmutableArray<>`/`IReadOnlyList<>`.
- For detected non-deterministic APIs, provides a diagnostic message with suggested refactorings (no automatic fix is applied).
---

## Implementation notes

- **Symbol discovery**
  - Cached at `CompilationStart`: `IMagikBlock<,>`, `ISelectionStrategy`, `Tag` type, `[CovenImmutable]` attribute symbol.
- **Performance**
  - SymbolStart per candidate block type; minimal data‑flow analysis limited to lambdas.
  - No project‑wide scanning of string literals or heavy control‑flow on every method.
- **Resilience**
  - Analyzer references types by **metadata name** (e.g., `Coven.Core.IMagikBlock\`2`) to tolerate namespace moves.
  - If symbols are not found, affected rules gracefully no‑op.

---

## Testing

Use the Roslyn SDK test harness (`Microsoft.CodeAnalysis.CSharp.CodeFix.Testing`) with xUnit or MSTest.

- **COV001**
  - Flags mutable `class`/`struct` as `TIn` with a single diagnostic; fix converts to `record` / `readonly record struct`.
  - Accepts `record` with `init;` properties and built‑in primitives.
  - Respects `[CovenImmutable]` and `.editorconfig` allow‑list.
- **COV002**
  - Flags instance state on `ISelectionStrategy` types (settable properties, non-`readonly` fields, mutable collection fields).
  - Flags non-deterministic API usage inside the strategy’s select method(s) (e.g., `DateTime.Now`, `Random`, `Guid.NewGuid`, `Environment.TickCount`).
  - Fix adds `sealed` and `readonly`, converts setters to `init` or get-only; nondeterminism warnings are diagnostic-only.
- **COV003**
  - Flags `Tag.*` calls in constructors, field initializers, and non‑`DoMagik` helpers.
  - Allows `Tag.*` inside `DoMagik` of any `IMagikBlock<,>`.

---

## CI & distribution

- **CI:** Run analyzer tests on every PR; consider a solution‑wide `dotnet build /warnaserror:$(COVEN_WARN_AS_ERROR)` job to enforce analyzer severities.
- **NuGet:** Ship `Coven.Analyzers` with `Analyzer` build assets so projects get diagnostics by adding a PackageReference.
- **VSIX (optional):** For local Visual Studio installation; NuGet is the primary distribution.

---

## Versioning & compatibility

- Analyzer IDs are stable: **COV001**, **COV002**, **COV003**.
- Target minimum language version **C# 9** (for `record` and `static` lambdas). Rules degrade gracefully on older language versions (e.g., recommend get‑only properties where `init;` is unavailable).

---

## FAQ

**Q:** Are strings or primitive inputs allowed for `TIn`?  
**A:** Yes. They are considered immutable and pass COV001.

**Q:** What if my input type is immutable but not a `record`?  
**A:** Mark it with `[CovenImmutable]` or add the fully‑qualified type name to `coven_immutable_types` in `.editorconfig`.

**Q:** Can I use `Tag.*` from helper methods?  
**A:** Yes, if those helpers are invoked within `DoMagik` during a request. Calls that occur outside a block’s execution context (ctor, static initializer, etc.) are flagged by COV003.

---

## Appendix: Quick reference

| ID     | Title                                                | Default | Fix |
|--------|------------------------------------------------------|---------|-----|
| COV001 | MagikBlock input must be immutable                   | error   | yes |
| COV002 | Selection strategies (`ISelectionStrategy`) must be stateless & deterministic | warning | yes |
| COV003 | Tag API usage must be inside `DoMagik`               | warning | no  |

---
