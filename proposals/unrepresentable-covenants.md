# Proposal: Unrepresentable Covenants — Typed Tree + DSL

> **Status**: Proposed  
> **Created**: 2026-07-12

---

## Summary

Lift the covenant graph out of runtime-validated value assembly and into shapes the compiler checks. Today the tree (branches → journals → routes → terminals) is assembled from untyped `BranchManifest` values and opaque predicates, then checked by `CovenantBuilder.Validate` at `Done()` — every rule in that validator is a bad state we chose to detect instead of prevent.

This proposal applies a strict prevention ladder to each known bad state:

1. **Impossible** — encode in the C# type system (evidence tokens, typestate builders, fused registration).
2. **Compile-time scream** — where C# generics run out (type-level sets, exhaustiveness over open hierarchies), a source generator + Roslyn analyzer over the same fluent DSL.
3. **Runtime scream** — the shrunken remainder of `Validate()`. Every rule deleted from it is entropy removed.

The DSL stays embedded C# — an external config format would move errors *toward* runtime, the wrong direction.

---

## Motivation: bad states representable today

All of these compile, and most pass covenant validation:

| # | Bad state | Today's failure mode |
|---|-----------|---------------------|
| 1 | Route declared for a journal nobody connected | `KeyNotFoundException` from `entryToJournal` at pump build |
| 2 | Route from an entry type no manifest produces | Dead route, silent no-op |
| 3 | Transmuter registered as interface, resolved as concrete | Runtime validation error (Rule 4) — was a silent pump death before PR #64 |
| 4 | Two filtered routes with overlapping predicates | **Both fire** — with no declaration of whether the author meant alternatives or fan-out |
| 5 | Two agent leaves connected to one `AgentEntry` journal | Validates clean; both providers answer every prompt; tool entries fault non-tool siblings |
| 6 | `EnableTools()` without tool routes | Claude emits `AgentToolCall`; unknown-tool guard saves the hang, but the covenant is silently partial |
| 7 | `RouteFileSystemTools()` without `EnableTools()` | Dead routes, silent no-op |
| 8 | `EnableStreaming()` + `EnableTools()` | Runtime throw at registration |
| 9 | Manifest declares types the leaf never writes (or omits ones it does) | Nothing ties declaration to behavior; validation reasons over fiction |
| 10 | `DisposeAsync` on a never-started daemon | `InvalidOperationException` (`Stopped → Completed` invalid) |

(#1–4: `src/Coven.Core/Covenants/`; #5: every `*AgentSession` tails the shared journal filtering only `IDraft`/acks; #6–8: `ClaudeAgentsServiceCollectionExtensions`, `FileSystemCompanionCovenantExtensions`; #9: every `*CovenBuilderExtensions`; #10: `ContractDaemon.Transition`.)

---

## Design

### 1. Evidence-carrying connections (kills #1, #2)

`Connect` stops returning `void`-ish fluency and starts returning **evidence**. Manifests become generated types (§4) implementing marker interfaces per declared entry:

```csharp
// Generated from the Chat branch's entry surface:
public sealed class ChatManifest : BranchManifest<ChatEntry>,
    IProduces<ChatAfferent>, IConsumes<ChatEfferent>, IConsumes<ChatEfferentDraft> { }
```

Routes then demand the evidence in their signature:

```csharp
Connected<ChatManifest> chat = covenant.Connect(coven.UseConsoleChat(config));
Connected<ClaudeManifest> agents = covenant.Connect(coven.UseClaudeAgents(config));

covenant.Route(from: chat, to: agents,
    (ChatAfferent msg, CancellationToken ct) => Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));
// Route<TFrom, TTo, TSource, TTarget> where TFrom : IProduces<TSource>, TTo : IConsumes<TTarget>
```

You cannot name a route whose endpoints aren't connected — the token doesn't exist. You cannot route from a type the source branch doesn't produce — the constraint fails. Validation Rules 1 and 3 (coverage, consumer satisfaction) become partially structural; what remains of them moves to the analyzer (§5).

### 2. One dispatcher per source journal — declared arbitration (kills #4)

The hazard in today's model is not multi-match itself — journal-first machinery legitimately fans one event out to many consumers (projections, ledgers, scorers, telemetry all derive from the same entries; replay-under-a-different-consumer is a first-class operation). The hazard is that **alternatives and fan-out are spelled identically**: N filtered routes with opaque predicates, and the semantics — both fire — is discoverable only at runtime.

Replace the undifferentiated route pile with **one dispatcher per source journal** carrying two declared case forms:

- **`On<T>`** — exclusive consumption. Within the `On` group for an entry type, semantics are ordered first-match: these are *alternatives*, and at most one fires.
- **`Tee<T>`** — declared fan-out. Every matching `Tee` fires, always, independent of and in addition to the `On` resolution. Tees are the multi-match surface: ledgers, telemetry, audit projections, secondary scorers.

```csharp
covenant.Dispatch(agents, d => d
    .On<AgentToolCall>(call => FileSystemCompanionRouting.IsValidReadFileCall(call),
        to: filesystem, AgentToolCallToFileRead)
    .On<AgentToolCall>(to: agents, InvalidReadFileCallToAgentToolFailure)   // ordered alternative
    .Tee<AgentToolCall>(to: ledger, ToolCallToLedgerEntry)                  // fan-out: always fires too
    .On<AgentResponse>(to: chat, r => new ChatEfferent("BOT", r.Text))
    .Terminal<AgentThought>());
```

Per entry: **all** matching `Tee` routes fire (order-independent, so they must not depend on each other), plus **at most one** `On` route (first match in declaration order). Value-level predicates can't be proven disjoint by any type system, so we don't try — ordering makes `On` overlap harmless by construction, and `Tee` makes intentional multi-match a visible declaration instead of an emergent property of predicate shapes.

Coverage composes: an entry type is satisfied by ≥1 `On`, or by `Terminal`; `Tee`s never consume, so `Tee<T>` + `Terminal<T>` expresses "observed by projections, routed nowhere" — the telemetry pattern. The generated `IEntryCases<TJournal>` interface (§4) carries one member per entry type in the branch, so an unhandled entry type is a compile error, and adding an entry type to a branch breaks every dispatcher that consumes it — the change screams at compile time instead of silently terminal-ing.

### 3. Fused route + registration, and registration typestate (kills #3, #6, #7, #8)

**Fusion**: `Route`/`On` overloads that take a transmuter type register the concrete type themselves at `Done()`. The covenant registers exactly what it resolves; Rule 4 is deleted, not tightened.

**Typestate**: provider registration becomes a linear builder. Mutually exclusive capabilities live on different types:

```csharp
public sealed class ClaudeRegistration
{
    public StreamingClaudeRegistration EnableStreaming() => new();   // no EnableTools member
    public ToolClaudeRegistration EnableTools() => new();            // no EnableStreaming member
}
```

`EnableTools()` yields a **capability token**, and the companion demands it:

```csharp
ToolCapability<ClaudeManifest> tools = registration.EnableTools();
covenant.RouteFileSystemTools(tools, filesystem);
```

Tools-without-routing can't happen (the token's only consumer is a routing call — an unused token is an analyzer diagnostic, CS-style "unused local" for capabilities). Routing-without-tools can't happen (no token, no call). Streaming+tools can't happen (no such method). Three runtime checks deleted.

### 4. Derived manifests — one source of truth (kills #9)

Manifests are hand-written today and already lie (streaming leaves write `AgentStreamCompleted`/acks that no manifest mentions). Invert the dependency: leaves declare writes structurally, manifests are **generated**:

```csharp
internal sealed class ClaudeAgentSession :
    IProduce<AgentResponse>, IProduce<AgentThought>, IProduce<AgentToolCall>,
    IConsume<AgentPrompt>, IConsume<AgentToolResult>, IConsume<AgentToolFailure> { ... }
```

A source generator emits `ClaudeManifest` (§1's evidence type) from these markers. A leaf that writes an entry type it doesn't implement `IProduce<>` for fails an analyzer check that inspects `IScrivener<T>.WriteAsync` call sites. Declared and actual cannot diverge because there is only one declaration.

### 5. Analyzer tier — where the type system runs out

C# cannot express type-level sets ("the connected-journal set contains `AgentEntry` at most once") or exhaustiveness over open hierarchies. A `Coven.Analyzers` package covers the gap at compile time:

| Diagnostic | Catches | Kills |
|------------|---------|-------|
| COVEN001 | Two `Connect`ed manifests share a `JournalEntryType` without explicit `SharedJournal(...)` opt-in | #5 |
| COVEN002 | `ToolCapability` obtained but never consumed by a routing call | #6 |
| COVEN003 | Dispatcher missing a case for a produced entry type (until generated `IEntryCases` fully lands) | #4 residue |
| COVEN004 | `ITransmuter` implementation injecting `IScrivener<>` (side-effect purity violation) | doc-honesty gap |
| COVEN005 | Leaf writes entry type without `IProduce<>` marker | #9 residue |
| COVEN006 | Unreachable `On<T>` case (declared after an unconditional `On<T>` for the same type) | ordered-alternatives footgun |

### 6. Runtime remainder

- Daemon lifecycle stays runtime (typestate handles for daemons are not worth the ceremony), but `Stopped → Completed` becomes a legal no-op — dispose-without-start is cleanup, not a protocol violation (#10).
- Gateway/API failures stay runtime and keep the `Status.Failed` → ritual propagation spine.
- `Validate()` shrinks to: journal-sharing check (backstop for hosts without the analyzer) and route/terminal exclusion. Each surviving rule documents *why* it can't move up the ladder.

---

## Full DSL sketch (sample 02, after)

```csharp
services.BuildCoven(coven =>
{
    var chat = coven.UseConsoleChat(consoleConfig);
    var (agents, tools) = coven.UseClaudeAgents(claudeConfig, r => r.EnableTools());
    var filesystem = coven.UsePosixFileSystem(fs => fs.Root = fsRoot);

    coven.Covenant(c =>
    {
        var chatC = c.Connect(chat);
        var agentsC = c.Connect(agents);
        var fsC = c.Connect(filesystem);

        c.Dispatch(chatC, d => d
            .On<ChatAfferent>(to: agentsC, msg => new AgentPrompt(msg.Sender, msg.Text)));

        c.Dispatch(agentsC, d => d
            .RouteFileSystemTools(tools, fsC)                       // consumes the capability token
            .On<AgentResponse>(to: chatC, r => new ChatEfferent("BOT", r.Text))
            .Tee<AgentResponse>(to: auditC, r => new AuditEntry(r)) // declared fan-out
            .Terminal<AgentThought>());

        c.Dispatch(fsC, d => d
            .On<FileContent>(to: agentsC, FileContentToAgentToolResult)
            .On<FileFailure>(to: agentsC, FileFailureToAgentToolFailure));
    });
});
```

Every wiring mistake enumerated in Motivation is now a red squiggle, not a 2 a.m. hang.

---

## Migration

Breaking — target a major version. Staged so each stage ships value alone:

1. **Stage 1 (pure C#, no codegen)**: `Connected<T>` tokens, fused route+registration (delete Rule 4), registration typestate + `ToolCapability`, legalize dispose-without-start. Old `Routes(c => ...)` surface kept `[Obsolete]` for one minor.
2. **Stage 2 (generator)**: `IProduce<>`/`IConsume<>` markers, generated manifests, generated `IEntryCases<TJournal>`, `Dispatch` replaces filtered-route fan-out.
3. **Stage 3 (analyzer)**: `Coven.Analyzers` with COVEN001–005, wired into `Directory.Build.props`.
4. **Stage 4 (deletion)**: remove superseded validation rules and the runtime throw for streaming+tools. Deleted code is the metric of success.

## Checklist

- [ ] `Connected<TManifest>` evidence tokens; `Route` constrained by `IProduces<>`/`IConsumes<>`
- [ ] Fused transmuter registration at `Done()`; delete validation Rule 4
- [ ] Registration typestate per provider; `ToolCapability<TManifest>`; delete streaming+tools throw
- [ ] `Stopped → Completed` as no-op transition
- [ ] Source generator: manifests from `IProduce<>`/`IConsume<>` markers
- [ ] Source generator: `IEntryCases<TJournal>` + `Dispatch` builder (`On` = ordered first-match alternatives; `Tee` = declared always-fire fan-out)
- [ ] `Coven.Analyzers`: COVEN001–COVEN006
- [ ] Shrink `Validate()`; document why each surviving rule is runtime-only
- [ ] Migrate samples/toys/E2E to the new DSL; delete `[Obsolete]` surface

## Related Proposals

- [Flat Covenant Model](unified-covenant-builder.md) — this proposal keeps the flat graph and hardens its assembly; no return of inner covenants.
- [Spellcasting](spellcasting-branch.md) — companions become capability-token consumers; the multi-tool dispatch question resolves into `Dispatch` ordering.
- [Claude Streaming Tool Calling](claude-streaming-tool-calling.md) — when implemented, the typestate split (streaming xor tools) relaxes into a merged state type rather than a runtime flag.
