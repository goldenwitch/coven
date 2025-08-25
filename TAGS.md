# Tagging Design

This document explains the initial tagging model and how it integrates with the Board’s compiled pipelines to enable tag-influenced routing without changing the `IMagikBlock<T,TOutput>` interface.

## Goals
- Preserve compiled pipeline delegates for performance and predictability.
- Allow tags to influence routing decisions at runtime between multiple valid next steps.
- Keep the block interface unchanged; avoid DI or explicit tag parameters.
- Maintain deterministic behavior and shortest-path guarantees; avoid loops.

## Terminology
- Tag: a case-insensitive string (e.g., `route:discord`, `role:planner`, `by:StringLengthBlock`).
- TagSet: an unordered, case-insensitive set of tags (duplicates collapse).

## Tag Context
- The Board owns a per-request tag scope implementing `ITagScope`.
- An ambient adapter `Tag` uses an `AsyncLocal<ITagScope?>` to point to the active scope. The Board brackets each `PostWork` with `Tag.BeginScope(scope)`/`Tag.EndScope(prev)`.
- Blocks can add tags at any time by calling `Tag.Add(...)`, and can check with `Tag.Contains(...)`, without any changes to `IMagikBlock`.

## Default Emission
- After each block runs, the Board appends a default observability tag: `by:<BlockTypeName>`.
- Default emission is for tracing only; it does not change selection logic.

## Routing Semantics
After each `MagikBlock` finishes, the Board selects the next block to run:
1) Explicit direction (optional feature): if the TagSet contains a matching `to:<BlockTypeName>` or `to:#<registryIndex>`, and the candidate accepts the current value type and appears after the last executed block, choose it.
2) Capability market: otherwise, choose the candidate with the highest overlap count between the current TagSet and the candidate’s advertised `SupportedTags`.
3) Default order: if capability scores tie (or no capabilities are advertised), pick the next registered block that accepts the current value type.

Notes:
- Forward-only: selection is restricted to blocks with a greater registry index than the last executed block. This ensures progress and prevents cycles.
- If no suitable next block exists and the current value is not assignable to the requested `TOutput`, the Board throws an error.
- No tags present means behavior defaults to “next registered that accepts the current type.”

## Compiled Router Pipelines
For each `(startType, targetType)`, the Board compiles a router delegate `Func<T, Task<TOutput>>` over an array of prebuilt candidates (registry order), where each candidate has a compiled invoker `Func<object, Task<object>>` and a pre-materialized, case-insensitive set of capability tags. The router:
- Initializes the TagSet for the work item.
- Starts from `lastIndex = -1` and current = input.
- Repeats:
  - If current is assignable to `TOutput`, return it.
  - Otherwise, pick the next block using explicit `to:*` (if present), or capability scoring (max overlap), with a tie broken by registration order; then await it.
  - Append `by:<BlockTypeName>`.
- A step cap (<= registry length) provides a hard stop if a target cannot be produced.

This preserves compiled performance while enabling dynamic per-step routing and supports multiple consecutive steps with the same input/output types.

## Determinism and Safety
- Forward-only selection yields monotonic progress through the registry and prevents most cycles. It is possible to cycle by repeatedly using the same block without an escape.
- Selection is deterministic given the TagSet and registry order.
- If no next block is available before reaching the target type, the Board throws a clear error.

## Out of Scope (for this phase)
- Tag override emitters per-block.
- `MagikTrick(...)` multi-branch blocks.
- Pull-mode tag-aware scheduling.

These can be layered on later without changing the fundamentals above.

## Builder Capability Assignment
- At registration, you can assign capability tags to a block via builder overloads. These are merged with any capabilities a block advertises via `ITagCapabilities`.

Example:

```
var coven = new MagikBuilder<string, double>()
    .MagikBlock((string s) => Task.FromResult(s.Length))
    .MagikBlock<int, double>(new IntToDoubleA(), new[] { "fast" }) // builder-assigned capability
    .MagikBlock<int, double>((int i) => Task.FromResult((double)i)) // fallback
    .Done();
```

## Selection Flow Summary
- Block emits tags with `Tag.Add("...")`.
- Router identifies forward-compatible candidates.
- If explicit `to:*` tags are present, they override.
- Else, pick the candidate with the highest capability overlap; tie-break on registration order.
- Board emits `by:<BlockTypeName>` after each step.
- Scoping: Tags are scoped to a single `PostWork` call; nested or concurrent executions maintain isolation via `AsyncLocal` scoping.
