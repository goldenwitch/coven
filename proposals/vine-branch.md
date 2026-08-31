# VINE Branch

> **Status**: Draft  
> **Created**: 2026-07-30

---

## Summary

Bring [Bacchus](https://github.com/goldenwitch/bacchus) task graphs into Coven so a single application can plan a project into a `.vine` file and then execute that plan with an agent.

Three additions, following the branch/leaf/companion shape already proven by `Coven.FileSystem`:

| Project | Role |
|---------|------|
| `Coven.Vine` | Branch — `VineEntry` journal, graph model, parser, serializer, validator, frontier |
| `Coven.Vine.FileStore` | Leaf — sandboxed `.vine` load and save with relative URI resolution |
| `Coven.Agents.Vine` | Companion — tool definitions and routes bridging agent tool calls to graph mutations |

---

## Motivation

### What Bacchus is

A TypeScript monorepo for authoring, validating, and visualizing task graphs in the [VINE format](https://github.com/goldenwitch/bacchus/blob/main/docs/VINE/v1.2.0.md) — a line-oriented plain-text DAG with a formal ABNF grammar. It ships five packages: `core` (parse/serialize/validate/query), `cli`, `mcp` (17 stdio MCP tools), `vscode` (VSIX bundling the MCP server), and `ui` (Svelte web app with a graph view and an AI chat planner). MIT licensed, same owner as Coven.

### The fit is unusually direct

Bacchus's chat planner independently arrived at the architecture Coven already provides. Its own [design doc](https://github.com/goldenwitch/bacchus/blob/main/docs/ChatPlanner.md) describes a provider-agnostic `ChatService`, a `ChatOrchestrator` running a tool-use loop, and a `GRAPH_TOOLS` table mapping validated mutations to LLM tool schemas.

| Bacchus chat planner | Coven equivalent |
|----------------------|------------------|
| `ChatService` — provider-agnostic LLM interface | `Coven.Agents` branch |
| `AnthropicChatService` — SSE streaming, tool blocks | `Coven.Agents.Claude` |
| `ChatOrchestrator` — send → tool calls → results → repeat | Covenant routes + agent daemon tool loop |
| `GRAPH_TOOLS` + `executeToolCall` | `Coven.Agents.Vine` companion |
| `@bacchus/core` mutations | `Coven.Vine` branch |

Integration is therefore mostly *deletion*: Coven supplies the orchestration layer Bacchus hand-rolled, and VINE supplies the domain Coven lacks.

### Why the graph matters for execution

`getActionableTasks` in `@bacchus/core` is already an agent execution engine. It is pure and read-only, and its doc comment describes the intended loop directly: call it, act on the results with mutation tools, call it again.

It returns a frontier of `ready`, `completable`, `blocked`, and `expandable` nodes. Two design choices in the format make this genuinely agent-ready rather than merely a to-do list:

- **`@guidance` and `@artifact` attachments.** Guidance is *"context or constraints on the work, available during review to verify work respected its limitations."* Artifacts are *"product of work; a reviewer examines artifacts to judge whether the task was completed."* That is a specification of what to inject into a prompt and what to check afterward.
- **The `reviewing` status.** A task moves to `reviewing` rather than straight to `complete`, and only becomes `completable` once a dependant starts consuming it. This implies a second pass — a reviewing agent distinct from the implementing one, which is precisely what a multi-agent engine is for.

---

## Design

### Port VINE to C# rather than calling Bacchus

Bacchus is Node-only. Three integration paths exist:

| Path | Cost | Verdict |
|------|------|---------|
| **Port `@bacchus/core` to C#** | Two implementations to keep in sync | **Recommended** |
| Spawn the `@bacchus/mcp` stdio server | Requires Node 22+ on every user machine, or bundling a runtime | Rejected for v1 |
| Both — port for rendering, MCP for mutation | Sync burden *and* a runtime dependency | Rejected |

Shipping a .NET desktop application that requires a Node installation to open a file is a poor trade, and bundling a runtime to render a graph is worse. The format is designed for porting: a formal ABNF grammar, published header regexes, and a documented step-by-step parsing algorithm. This is a few hundred lines with a ready-made conformance corpus.

An MCP **client** branch for Coven remains independently attractive — it would open the whole MCP ecosystem, not just Bacchus — but it is a separate proposal and should not gate this work.

### Entries

Modeled on [`FileSystemEntry`](../src/Coven.FileSystem/FileSystemEntry.cs): correlation-matched request/response, deliberately minimal.

```
BASE VineEntry : Entry

  EFFERENT (commands)
    VineLoad         { correlation-id, path }
    VineSave         { correlation-id, path }
    VineMutate       { correlation-id, operation }   -- add/remove/status/depend/ref
    VineQueryFrontier{ correlation-id }

  AFFERENT (results)
    VineGraphLoaded  { correlation-id, title, task-count }
    VineFrontier     { correlation-id, ready[], completable[], blocked[], expandable[], progress }
    VineMutated      { correlation-id, operation, task-count }
    VineFailure      { correlation-id, failure-kind, message }
```

Graph state lives in the branch, not in the journal. The journal records *operations and outcomes*, which keeps entries small and gives a replayable audit trail of how a plan evolved — the interesting artifact when an agent has been editing a plan unattended.

### Execution: the conductor

A daemon drives the frontier loop. Graph mutation stays in the branch; the conductor only decides what to hand out next.

```
DAEMON VineConductor
  tails: IScrivener<VineEntry>

  ON VineFrontier { ready, expandable, progress }:
    IF ready empty AND expandable empty:
      IF progress indicates root satisfied: WRITE VineRunCompleted
      ELSE:                                 WRITE VineRunStalled
      RETURN

    FOR EACH task IN ready, up to concurrency-limit:
      WRITE VineTaskReady { task-id, name, description, guidance[], artifacts[] }

  ON VineTaskOutcome { task-id, outcome }:
    WRITE VineMutate { set-status task-id -> reviewing | blocked }
    WRITE VineQueryFrontier
```

The covenant closes the loop, and every step is a declared route rather than hidden control flow:

```
VineTaskReady  ──▶ AgentPrompt        (guidance injected as context)
AgentResponse  ──▶ VineTaskOutcome
AgentToolCall  ──▶ FileRead / FileWrite / VineMutate
FileContent    ──▶ AgentToolResult
```

`VineRunStalled` is deliberate. A frontier that empties without satisfying the root means a `blocked` task needs intervention, and surfacing that as an entry is better than an agent silently idling.

### Companion tools

`Coven.Agents.Vine` mirrors [`Coven.Agents.FileSystem`](../src/Coven.Agents.FileSystem/README.md): tool definitions plus `RouteVineTools()`, with validity predicates on routes so malformed calls become `AgentToolFailure` instead of hanging.

Tool surface follows Bacchus's `GRAPH_TOOLS` — `get_graph`, `add_task`, `remove_task`, `set_status`, `update_task`, `add_dependency`, `remove_dependency`, `add_ref`, `expand_ref`, `add_attachment`, `remove_attachment`, `replace_graph`.

Bacchus's key insight carries over and should be preserved: the model manipulates the graph through **validated mutations**, never by emitting raw VINE text. Every mutation is validated before returning, and violations come back as readable error strings so the model self-corrects. Cycle and island constraints make free-text generation unreliable in a way structured mutation is not.

### Two modes in the desktop app

| Mode | Loop | Tools |
|------|------|-------|
| **Plan** | User converses; agent mutates the graph; graph view updates live | `Coven.Agents.Vine` |
| **Build** | Conductor walks the frontier; agent executes each task | `Coven.Agents.Vine` + `Coven.Agents.FileSystem` |

Build mode is where the journal inspector from [Coven UI Desktop](coven-ui-desktop.md) stops being a debugging aid and becomes the primary interface — an agent autonomously building a project is exactly the case where you need a complete, positioned record of what it did.

Build mode also makes the tool-approval gate load-bearing rather than optional. It was deferred from the UI v1 scope; autonomous execution against a real file system is the point at which it should return.

### Rendering

VINE ordering is *"mandatory and carries semantic meaning — the suggested reading and tackling order."* A layered top-down layout therefore matches the format's intent better than the force-directed physics view Bacchus's web UI uses, and it is substantially less work in Avalonia. Root at top, dependencies descending, status by color.

---

## Risks

**Two implementations drift.** The real cost of porting. Mitigation: Bacchus's [`examples/`](https://github.com/goldenwitch/bacchus/tree/main/examples) folder is ten files spanning every format feature, and makes a natural cross-language conformance corpus. Best contributed back to Bacchus as language-neutral fixtures with expected parse results, so both implementations test against one source of truth.

**The code is ahead of the spec.** Connective nodes (`anyof` / `allof`) are implemented in `@bacchus/core` with a `CONNECTIVE_HEADER_RE`, and participate in the satisfaction predicate — an `anyof` is satisfied when any dependency is, an `allof` when all are. But the newest published spec is v1.2.0, where connectives exist only as a proposal document. A port must target the code, not the spec. Bacchus should mint v1.3.0 first.

**The v1.2.0 spec contains an invalid example.** The "With Annotations" example declares `root -> backend` and `backend -> root`, a two-node cycle that violates the spec's own constraint 4 (*no cycles*). The "Nested/annotation" variant has the same shape with the `frontend` ref. Worth fixing upstream — a conformance corpus built from the spec would fail on it.

**Licensing.** Bacchus is MIT, Coven is BUSL-1.1. MIT into BUSL is fine, and both are goldenwitch-owned. Ported code must retain the MIT notice; add an attribution entry to [NOTICE](../NOTICE).

---

## Scope

**In scope:**
- `Coven.Vine`: graph model, parser, serializer, validator, frontier (`getActionableTasks` equivalent), expansion
- Connective nodes (`anyof` / `allof`) to match current `@bacchus/core` behavior
- `Coven.Vine.FileStore` leaf: sandboxed load/save, relative URI resolution
- `Coven.Agents.Vine` companion: tool definitions, `RouteVineTools()`, validity predicates
- `VineConductor` daemon and execution entries
- Conformance tests against Bacchus's `examples/` corpus
- Layered DAG rendering, plan and build modes in the desktop app

**Out of scope:**
- MCP client branch — separate proposal, independently valuable
- Writing VINE text directly from the model (structured mutations only)
- Reimplementing Bacchus's physics layout
- Changes to the VINE format itself
- Multi-user or concurrent editing of one `.vine` file

---

## Dependencies

- [Coven UI Desktop](coven-ui-desktop.md) — host application, journal inspector, approval gate
- [`Coven.FileSystem`](../src/Coven.FileSystem/README.md) and [`Coven.Agents.FileSystem`](../src/Coven.Agents.FileSystem/README.md) — precedent, and required for build mode
- [Claude Streaming Tool Calling](claude-streaming-tool-calling.md) — required for streaming *with* tools, which both modes want
- Bacchus VINE v1.3.0 minted to cover connectives

---

## Checklist

- [ ] `VineEntry` hierarchy with `[JsonPolymorphic]`
- [ ] Parser: magic line, preamble, delimiter, task / ref / connective blocks, annotations
- [ ] Serializer: canonical form, roundtrip-stable
- [ ] Validator: unique IDs, valid refs, no cycles, no islands, no attachments on refs
- [ ] Frontier: satisfaction predicate with connectives, ready / completable / blocked / expandable, progress
- [ ] Expansion: prefix remapping, root adoption, dependency and decision merging
- [ ] `Coven.Vine.FileStore` leaf with path sandboxing
- [ ] `Coven.Agents.Vine`: tool definitions, `RouteVineTools()`, validity predicates
- [ ] `VineConductor` daemon with concurrency limit, `VineRunCompleted` / `VineRunStalled`
- [ ] Conformance tests over Bacchus `examples/`, roundtrip-stable
- [ ] Layered DAG view in the desktop app
- [ ] Plan mode and build mode
- [ ] Tool-approval gate active in build mode
- [ ] `NOTICE` attribution for ported MIT code
- [ ] Upstream: report the cyclic spec examples to Bacchus
- [ ] Upstream: propose language-neutral conformance fixtures to Bacchus
