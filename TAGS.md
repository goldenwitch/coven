# Tagging Design

This document explains the initial tagging model and how it integrates with the Board’s compiled pipelines to enable tag-influenced routing without changing the `IMagikBlock<T,TOutput>` interface.

## Goals
- Preserve compiled pipeline delegates for performance and predictability.
- Allow tags to influence routing decisions at runtime between multiple valid next steps.
- Keep the block interface unchanged.
- Maintain deterministic behavior.

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
- Forward-only: For push-mode selection is restricted to blocks with a greater registry index than the last executed block. This ensures progress and prevents cycles.
- If no suitable next block exists and the current value is not assignable to the requested `TOutput`, the Board throws an error.
- No tags added means that the dominant scheme will be auto-tagging indicating the next block.

## Compiled Router Pipelines
For each `(startType, targetType)`, the Board compiles a router delegate `Func<T, Task<TOutput>>` over an array of prebuilt candidates (registry order), where each candidate has a compiled invoker `Func<object, Task<object>>` and a pre-materialized, case-insensitive set of capability tags. The router:
- Initializes the TagSet for the work item.
- Starts from `lastIndex = -1` and current = input.
- Repeats:
  - Pick the next block using explicit `to:*` (if present), or capability scoring (max overlap), with a tie broken by registration order; then await it.
  - Append `by:<BlockTypeName>`.
- When no next step exists, if the final value is assignable to `TOutput` return it; otherwise throw.

This preserves compiled performance while enabling dynamic routing.

## Determinism and Safety
- Forward-only selection yields monotonic progress through the registry and prevents most cycles. It is possible to cycle by repeatedly using the same block without an escape.
- Selection is deterministic given the TagSet and registry order.
- If no next block is available before reaching the target type, the Board throws a clear error. This should be impossible given our type-safe builder, but I know that some of y'all can do the impossible.

## Out of Scope (for this phase)
- Tag override emitters per-block.
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

## Custom Selection Strategy

Advanced users can override the routing algorithm by supplying a custom `ISelectionStrategy` through the builder:

```
var coven = new MagikBuilder<string, string>()
    .UseSelectionStrategy(new MySelectionStrategy())
    .MagikBlock(new StepA())
    .MagikBlock(new StepB())
    .Done();
```

If not provided, the `DefaultSelectionStrategy` applies the rules described above.

## Canonical Magical Tags

These tags have special, built-in behavior in the router. Use sparingly.

- `to:#<index>`: Explicitly select the next block by registry index. Overrides all other selection.
- `to:<BlockTypeName>`: Explicitly select the next block by type name. Overrides all other selection.

Notes:

- Non-magical tags (e.g., `want:*`, `role:*`, `route:*`) remain user-defined context/capability markers. The selector primarily considers tags emitted in the immediately preceding step.
- We do not plan to add more magical tags. Over time we intend to reduce and remove magic tags where practical in favor of explicit APIs (like internal selection fences) and capability-based routing.

## Step-Scoped Tag Semantics (Implementation Detail)

Internally, the Board journals tags by step (epoch) to enable “next-hop” decisions without destructive mutation:

- Tags added by a block are stamped with the next epoch so they apply to the following selection.
- This preserves deterministic routing while keeping observability tags (like `by:*`) available in the full tag set.
