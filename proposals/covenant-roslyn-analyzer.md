# Proposal: Covenant Roslyn Analyzer

## Problem Statement

The Coven.Covenants framework provides runtime validation of covenant graphs through `CovenantValidator`. This validation runs at application startup and catches configuration errors like:

- **Dead letters**: Entry types that are produced but never consumed
- **Orphaned consumers**: Entry types consumed but never produced
- **Islands**: Sinks unreachable from any source
- **Missing boundaries**: Covenants without sources or sinks

The problem: **these errors are only discovered at runtime**. A developer can write a complete application with an invalid covenant graph, get clean compilation, and only discover the error when the application starts (or worse, in production).

## Proposed Solution

A Roslyn analyzer (`Coven.Covenants.Analyzers`) that performs compile-time verification of covenant graphs by analyzing:

1. Types implementing `ICovenantEntry<T>`, `ICovenantSource<T>`, `ICovenantSink<T>`
2. Calls to `AddCovenant<T>()` and the builder methods within
3. The connectivity of the resulting graph

## What The Analyzer Should Verify

### Diagnostic: COVEN001 — Dead Letter

**Severity**: Error

An entry type implements `ICovenantEntry<TCovenant>` but:
- Is not consumed by any `Window<TIn, _>`, `Transform<TIn, _>`, or `Junction<TIn>` where `TIn` is that type
- Does not implement `ICovenantSink<TCovenant>`

```csharp
// COVEN001: 'ChatEfferent' is produced but never consumed and is not a sink
public record ChatEfferent(string Text) 
    : ChatEntry, ICovenantEntry<ChatCovenant>;
```

### Diagnostic: COVEN002 — Orphaned Consumer

**Severity**: Error

A builder method consumes a type that:
- Is not produced by any `Window<_, TOut>`, `Transform<_, TOut>`, or junction route
- Does not implement `ICovenantSource<TCovenant>`

```csharp
// COVEN002: 'ChatChunk' is consumed but never produced and is not a source
covenant.Window<ChatChunk, ChatEfferent>(policy, transmuter);
```

### Diagnostic: COVEN003 — Missing Source

**Severity**: Error

A covenant has no types marked with `ICovenantSource<TCovenant>` and no `Source<T>()` registrations.

```csharp
// COVEN003: Covenant 'ChatCovenant' has no sources
services.AddCovenant<ChatCovenant>(covenant =>
{
    covenant.Sink<AssistantMessage>();
});
```

### Diagnostic: COVEN004 — Missing Sink

**Severity**: Error

A covenant has no types marked with `ICovenantSink<TCovenant>` and no `Sink<T>()` registrations.

```csharp
// COVEN004: Covenant 'ChatCovenant' has no sinks
services.AddCovenant<ChatCovenant>(covenant =>
{
    covenant.Source<UserMessage>();
});
```

### Diagnostic: COVEN005 — Unreachable Sink (Island)

**Severity**: Error

A sink exists that is not reachable from any source through the graph of windows, transforms, and junctions.

```csharp
// COVEN005: Sink 'OrphanedOutput' is not reachable from any source
services.AddCovenant<ChatCovenant>(covenant =>
{
    covenant.Source<UserMessage>();
    covenant.Sink<AssistantMessage>();  // Reachable via transforms
    covenant.Sink<OrphanedOutput>();    // Not connected to anything
});
```

### Diagnostic: COVEN006 — Entry Without Covenant Registration

**Severity**: Warning

A type implements `ICovenantEntry<TCovenant>` but no `AddCovenant<TCovenant>()` call exists in the solution.

```csharp
// COVEN006: 'ChatMessage' implements ICovenantEntry<ChatCovenant> but ChatCovenant is never registered
public record ChatMessage(string Text) : ICovenantEntry<ChatCovenant>;
```

### Diagnostic: COVEN007 — Source/Sink Without Entry

**Severity**: Error

A type implements `ICovenantSource<T>` or `ICovenantSink<T>` without also implementing `ICovenantEntry<T>`.

```csharp
// COVEN007: 'BadSource' implements ICovenantSource<ChatCovenant> but not ICovenantEntry<ChatCovenant>
public record BadSource(string Text) : ICovenantSource<ChatCovenant>;
```

## Implementation Approach

### Option A: Roslyn Analyzer (Recommended)

A standard Roslyn DiagnosticAnalyzer that:

1. Registers syntax/symbol actions for:
   - Type declarations implementing covenant interfaces
   - Invocations of `AddCovenant<T>()`
   - Method calls on `IStreamingCovenantBuilder<T>`

2. Builds an in-memory graph during analysis

3. Reports diagnostics when graph invariants are violated

**Pros**:
- Real-time feedback in the IDE
- Works with incremental compilation
- Can suppress individual diagnostics
- Familiar analyzer infrastructure

**Cons**:
- Analyzing cross-file relationships is complex
- Lambda analysis for `AddCovenant` configuration requires careful handling

### Option B: Source Generator

A Roslyn Source Generator that:

1. Scans for all `ICovenant` implementations
2. Collects all `ICovenantEntry<T>` types
3. Analyzes `AddCovenant` calls via syntax analysis
4. Generates a compile-time validator that fails compilation on invalid graphs

**Pros**:
- Full compilation context available
- Can generate additional metadata/documentation

**Cons**:
- Less immediate feedback than analyzers
- Heavier compilation overhead

### Recommended: Analyzer with Compilation-End Action

Use a `CompilationStartAction` that:
1. Accumulates covenant metadata across the compilation
2. In `CompilationEndAction`, validates the complete graph
3. Reports diagnostics with accurate locations

This balances real-time feedback with cross-file analysis needs.

## Integration with Existing Interfaces

The analyzer leverages the existing marker interface hierarchy:

```
ICovenant
├── static abstract Name  → Identifies the covenant for diagnostics
│
ICovenantEntry<TCovenant>
├── Base membership       → Entry participates in graph
│
ICovenantSource<TCovenant>  
├── Boundary marker       → Entry doesn't need internal producer
│
ICovenantSink<TCovenant>
├── Boundary marker       → Entry doesn't need internal consumer
```

The analyzer must understand the builder API:

```csharp
IStreamingCovenantBuilder<T>.Source<TEntry>()   // Registers a source
IStreamingCovenantBuilder<T>.Sink<TEntry>()     // Registers a sink
IStreamingCovenantBuilder<T>.Window<TIn, TOut>() // Consumes TIn, produces TOut
IStreamingCovenantBuilder<T>.Transform<TIn, TOut>() // Consumes TIn, produces TOut
IStreamingCovenantBuilder<T>.Junction<TIn>()    // Consumes TIn, produces route outputs
```

## Package Structure

```
Coven.Covenants.Analyzers/
├── Coven.Covenants.Analyzers.csproj
├── CovenantAnalyzer.cs           # Main analyzer
├── CovenantGraphBuilder.cs       # Builds graph from syntax
├── Diagnostics/
│   ├── DiagnosticDescriptors.cs  # COVEN001-007 definitions
│   └── DiagnosticMessages.resx   # Localized messages
└── README.md
```

The package should be referenced as an analyzer:

```xml
<PackageReference Include="Coven.Covenants.Analyzers" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

## Relationship to Runtime Validation

The analyzer does **not** replace `CovenantValidator`. Both serve distinct purposes:

| Aspect | Roslyn Analyzer | Runtime Validator |
|--------|-----------------|-------------------|
| When | Compile time | Application startup |
| Scope | Static graph | Static + dynamic registrations |
| Dynamic covenants | Cannot verify | Full verification |
| Error handling | IDE squiggles, build errors | Exceptions with context |

Projects using dynamic covenant registration (runtime tool discovery, plugin systems) would rely primarily on the runtime validator. The analyzer catches what can be statically determined.

## Success Criteria

1. All diagnostics mirror `CovenantValidator` checks
2. Zero false positives on valid covenant graphs
3. Diagnostics appear in real-time during editing
4. Works with multi-project solutions
5. Performance: < 100ms additional analysis time for typical solutions

## Open Questions

1. **Suppression granularity**: Should diagnostics be suppressible per-entry, per-covenant, or globally?

2. **Partial covenants**: How to handle covenants that are intentionally incomplete at compile time (completed via runtime registration)?

3. **Test projects**: Should analyzer run on test projects, or only production code?

4. **Code fixes**: Should the analyzer offer code fixes (e.g., "Add ICovenantSink<T>" for dead letters)?

## References

- [CovenantValidator.cs](../src/Coven.Covenants/CovenantValidator.cs) — Runtime validation logic
- [Journal-Protocol-Isolation.md](../architecture/Journal-Protocol-Isolation.md) — Covenant design
- [Marker interfaces](../src/Coven.Core/Covenants/) — `ICovenant`, `ICovenantEntry<T>`, etc.
