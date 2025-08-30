# Unified Tagging & Routing Model

This document defines how tags are scoped, emitted, and used to select the next block in a compiled pipeline—while keeping the `IMagikBlock<T, TOutput>` interface unchanged and preserving deterministic, high‑performance routing.

---

## Goals

- Preserve compiled pipeline delegates for performance and predictability.  
- Allow tags to influence routing decisions at runtime between multiple valid next steps.  
- Keep the block interface unchanged and maintain deterministic behavior.

---

## Terminology

- **Tag**: A case‑insensitive string (e.g., `route:discord`, `role:planner`, `by:StringLengthBlock`).  
- **TagSet**: An unordered, case‑insensitive set of tags (duplicates collapse).

---

## Tag Scope & Ambient Access

- Tags are scoped to a single Board request: the Board creates a per‑request **tag scope**, and a static helper `Tag` points to it for the duration of `PostWork`. Blocks can add or check tags at any time via `Tag.Add(...)` and `Tag.Contains(...)`.
- Implementation detail: the ambient adapter uses `AsyncLocal<ITagScope?>`. The Board brackets each `PostWork` with `Tag.BeginScope(scope)` / `Tag.EndScope(prev)`.

---

## Capabilities on Blocks

- Blocks may **advertise capability tags** via `ITagCapabilities.SupportedTags`.
- Builder overloads can assign additional capability tags at registration; these merge with the block‑advertised capabilities.

**Example** (builder‑assigned capability plus fallback):

```csharp
ICoven coven = new MagikBuilder<string, double>()
    .MagikBlock((string s) => Task.FromResult(s.Length))
    .MagikBlock<int, double>(new IntToDoubleA(), new[] { "fast" }) // builder capability
    .MagikBlock<int, double>(new IntToDoubleB())                    // fallback
    .Done();
```

(Any comparable example with a lambda fallback is equivalent in spirit.)

---

## Default Tags Emitted by the Board

After each block runs, the Board:

1) Appends **`by:<BlockTypeName>`** for observability/tracing only; `by:*` does **not** affect capability scoring.  
2) Injects **forward‑bias hints**: `next:<DownstreamBlockTypeName>` for all forward‑compatible/reachable downstream candidates. These are **soft hints**: they contribute to capability overlap but do not override more specific capabilities.

> **Strategy hook:** Presence of any `by:*` (i.e., “after the first hop”) may cause the default strategy to **prefer a `MagikTrick<T>` candidate** on that hop if present; tricks fence the next selection to a predefined candidate set, with capability matching and registration order still resolving ties inside the fenced set.

---

## Routing Algorithm (Per Step)

Given the current epoch’s tags (tags added during the immediately preceding step, including forward hints):

1) **Explicit direction wins**: if `to:#<index>` or `to:<BlockTypeName>` is present, and the candidate both accepts the current value type **and** appears **after** the last executed block, choose it. This overrides other rules.  
2) **Capability overlap**: otherwise, pick the candidate with the highest overlap between the current TagSet and the candidate’s advertised capabilities (including builder‑assigned). Forward hints `next:*` participate in this overlap to bias progress.  
3) **Tie‑break**: if scores tie (or no capabilities are advertised), choose the next registered block (by order) that accepts the current value type.

**Forward‑only selection**: In push mode, selection is restricted to blocks with a greater registry index than the last executed block. This ensures progress and prevents most cycles. If no suitable next block exists and the current value is not assignable to `TOutput`, the Board throws.

---

## Compiled Router Pipelines

For each `(startType, targetType)`, the Board compiles a router delegate over a prebuilt array of candidates in registration order. The router initializes the TagSet, then repeatedly selects and invokes the next candidate using the rules above, appending `by:*` and injecting `next:*` hints after each step. When no next step exists, it returns the value if assignable to `TOutput`, otherwise throws. This preserves compiled performance with dynamic, tag‑influenced routing.

---

## Determinism & Safety

- Forward‑only selection yields monotonic progress and prevents most cycles (though repeating the same block without an escape can still cycle).  
- Selection is deterministic given the TagSet and registry order.  
- If the target type can’t be reached before candidates are exhausted, a clear error is thrown (should be unreachable with a type‑safe builder).

---

## Pull Mode

Pull mode advances one step at a time via `GetWork<TIn>(request)`:

- Each step emits `by:*` and computes forward `next:*` hints.  
- The step’s tags are **persisted to the branch**, excluding observability (`by:*`) and computed hints (`next:*`), and then the computed `next:*` hints are re‑added to bias the subsequent selection.  
- On the next step, the persisted branch tags become the current epoch’s tags, so explicit `to:*` and capability matching apply as in push mode.

---

## Magic Tags (Canonical)

- **`to:#<index>`**: Force the next block by registry index; overrides everything else (still must accept the current type and be forward in registration order).  
- **`to:<BlockTypeName>`**: Force the next block by type name; same constraints as above.  
- **`by:<BlockTypeName>`**: Emitted after each step for tracing only; does not affect selection.

> We do not plan to add more magical tags; long‑term direction favors explicit APIs and capability‑based routing.

---

## Step‑Scoped Tag Journal (Implementation Detail)

Internally, the Board journals tags by **epoch** (“step”) to enable next‑hop decisions without destructive mutation:

- Tags added by a block apply to the **following** selection (the “current epoch” for the next hop).  
- `by:*` is recorded for observability but filtered from persisted tags in pull mode.  
- `next:*` hints are computed/added after each step and re‑added during pull persistence to preserve forward bias.

---

## Selection Flow (At a Glance)

1) Block may emit tags with `Tag.Add("...")`.  
2) Router identifies forward‑compatible candidates and injects `next:*` hints.  
3) Explicit `to:*` overrides; else pick by capability overlap; break ties by registration order.  
4) Board appends `by:*` after each step.
