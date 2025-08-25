# Coven Agents: New Developer Guide

Welcome! This guide gives a fast, practical orientation to the Coven codebase so you can read the structure, run tests, and build features confidently. It focuses on what exists today.

## What Is Coven

Coven is an engine for orchestrating multiple AI coding agents to collaborate on complex tasks. It coordinates work by composing small typed units (MagikBlocks) into dynamic, tag-influenced pipelines executed by a Board. You typically build a Coven using a type-safe Builder, then call a single `Ritual<TIn, TOut>` entrypoint to run end-to-end workflows.

## Quick Start

Prerequisites
- .NET SDK that supports `net10.0` (latest SDK/preview may be required).

From repo root:
```bash
dotnet --info
dotnet restore
dotnet build
dotnet test src/Coven.Core.Tests -v minimal
```

If tests fail to run due to SDK/TFM mismatch, install a newer .NET SDK that supports `net10.0`.

## Repository Layout

- `src/Coven.Core/`
  - `Board.cs`: core push-mode router and compiled pipeline cache
  - `Builder/`: builder interfaces and implementation
  - `Tags/`: ambient tag scope, capability interface
  - `Algos/`: BFS helpers used by tests
  - `*.cs`: core abstractions (`IMagikBlock`, `MagikBlock`, `ICoven`, `IBoard`, `MagikBlockDescriptor`)
- `src/Coven.Core.Tests/`: unit tests covering routing, tags, capabilities, and end-to-end flows
- `README.md`: high-level concepts and examples
- `TAGS.md`: routing/tagging model details
- `TODO.md`: future ideas (for reference only)

## Core Concepts

MagikBlock
- Definition: `IMagikBlock<TIn, TOut>` with `Task<TOut> DoMagik(TIn input)`.
- Purpose: a single, typed transformation step (ideally pure/side-effect free).
- Adapter: `MagikBlock<TIn, TOut>` wraps a `Func<TIn, Task<TOut>>` if you prefer inline lambdas.

Builder
- Compose a registry of blocks and finalize with `.Done()` to get an `ICoven`.
- Supports homogeneous and heterogeneous registration (varying `TIn/TOut`).
- Can assign capability tags at registration time; merged with block-advertised capabilities.

Board
- Executes work in push mode by default (the implemented mode).
- Compiles and caches pipelines per `(startType, targetType)` for speed.
- Uses tags to influence routing among valid next steps.

Tags and Capabilities
- Ambient tag scope (per request) via `Coven.Core.Tags.Tag`.
  - `Tag.Add("...")`, `Tag.Contains("...")`, `Tag.Current` are only valid during Board execution.
- Blocks may advertise capabilities by implementing `ITagCapabilities.SupportedTags`.
- You can also assign capabilities to a block at registration time via the Builder.

Routing Rules (per step)
1. Explicit `to:*` tags win: `to:#<index>` (registry index) or `to:<BlockTypeName>`.
2. Otherwise choose candidate with highest overlap between `Tag.Current` and candidate’s capabilities.
3. Tie-break by registration order.
- Forward-only: once a block at registry index N runs, only candidates with index > N are considered.
- After each step the Board emits `by:<BlockTypeName>` for observability.
- If the current value is assignable to the requested `TOut`, the pipeline returns immediately.

## Minimal Examples

Single Step
```csharp
using Coven.Core.Builder;

var coven = new MagikBuilder<string, int>()
    .MagikBlock((string s) => Task.FromResult(s.Length))
    .Done();

int result = await coven.Ritual<string, int>("hello"); // 5
```

Heterogeneous Chain
```csharp
var coven = new MagikBuilder<string, double>()
    .MagikBlock((string s) => Task.FromResult(s.Length)) // string -> int
    .MagikBlock((int i) => Task.FromResult((double)i))   // int -> double
    .Done();

double d = await coven.Ritual<string, double>("abcd"); // 4d
```

Capability-Based Routing
```csharp
using Coven.Core.Tags;

sealed class EmitFast : IMagikBlock<string, int>
{ public Task<int> DoMagik(string s) { Tag.Add("fast"); return Task.FromResult(s.Length); } }

sealed class A : IMagikBlock<int, double> { public Task<double> DoMagik(int i) => Task.FromResult((double)i); }
sealed class B : IMagikBlock<int, double> { public Task<double> DoMagik(int i) => Task.FromResult((double)i + 1000d); }

var coven = new MagikBuilder<string, double>()
    .MagikBlock(new EmitFast())
    .MagikBlock<int, double>(new A(), new[] { "fast" }) // capability match preferred
    .MagikBlock<int, double>(new B())
    .Done();

double out1 = await coven.Ritual<string, double>("abc"); // 3d (routes to A)
```

Explicit Override with `to:*`
```csharp
// If your first step emits `Tag.Add("to:BlockTypeName")`, the router will pick it next
sealed class EmitNext : IMagikBlock<string, int>
{
    public Task<int> DoMagik(string s) { Tag.Add("to:IntToDoubleAddOne"); return Task.FromResult(s.Length); }
}
```

## Writing Blocks

Guidelines
- Prefer pure functions: given input -> produce output; avoid side effects.
- Use `async` when doing I/O or timers; the Board awaits each step.
- Inputs/outputs can be any types; immutable records are recommended for clarity.
- Throw exceptions to fail fast; they propagate to the caller.

Emitting Tags
- Only call `Tag.Add/Contains/Current` while your block is running under the Board. Outside that scope, these methods throw.
- Emit “intent” tags to influence selection (e.g., `style:loud`, `role:planner`).
- Use explicit direction with care: `to:#<index>` or `to:<TypeName>` affects only the next step.

Advertising Capabilities
- Implement `ITagCapabilities.SupportedTags` on a block to declare what tags it handles well.
- Or assign capabilities at registration: `.MagikBlock<TIn,TOut>(block, new[] { "fast" })`.

## Using the Builder

Common overloads
```csharp
var coven = new MagikBuilder<TStart, TEnd>()
    .MagikBlock(new SomeBlock())                          // instance
    .MagikBlock((TIn x) => Task.FromResult(...))          // inline func
    .MagikBlock<TIn, TOut>(new OtherBlock())              // heterogeneous
    .MagikBlock<TIn, TOut>((TIn x) => Task.FromResult(...))
    .MagikBlock<TIn, TOut>(new OtherBlock(), new[]{"tag"})// capabilities
    .Done();
```

`.Done()` returns an `ICoven` backed by a push-mode `Board` with precompiled pipelines.

## Working Style

- Single-feature focus: Work on one feature at a time until it is robust and generic enough to reuse. Avoid scattering partial implementations across the codebase.
- Tight iteration loop: add the smallest viable slice, write/extend tests, and keep APIs minimal.
- Prefer removing code over adding knobs; surface area should grow only when proven by usage.

## Documentation Scope

- README.md: Document public members, the high-level design, and functional behavior that users consume. Avoid detailing internal classes or private implementation notes here.
- AGENTS.md: Capture contributor-facing details (internal architecture, refactors, conventions, testing patterns) that help maintainers evolve the codebase safely.
- Code comments: Keep them concise and focused on intent where helpful; prefer tests and clear APIs over verbose internal docs.

## Running and Testing

Run all tests
```bash
dotnet test src/Coven.Core.Tests -v minimal
```

Helpful tests to read
- `TagRoutingTests.cs`: explicit `to:*` overrides and block-emitted direction.
- `TagCapabilityTests.cs`: capability-based selection and builder-assigned capabilities.
- `BoardChainTests.cs`: default order, async propagation, type assignability, precompilation.
- `HaloE2ETests.cs`: end-to-end example matching the README walkthrough.
- `TagScopeTests.cs`: tag scope lifetime and access rules.

Troubleshooting
- “No active tag scope.”: `Tag.*` called outside a Board-executed block.
- “No next step available…”: registry doesn’t contain a forward path from current type to the requested output.
- TFM/SDK issues: ensure your .NET SDK supports `net10.0`.

## Design Notes (Current Behavior)

- Mode: push mode is implemented; the Board dispatches work immediately and awaits completion step-by-step.
- Forward-only routing prevents most cycles and ensures progress by registry order.
- After each step the Board adds `by:<BlockTypeName>` to the tag set for observability; it doesn’t affect selection.
- Pipelines are compiled and cached per `(startType, targetType)` and can be precompiled across discovered types for faster first-run.

## Where to Add Things

- New blocks: `src/Coven.Core/*` (or a new project referencing `Coven.Core`).
- New builder patterns: `src/Coven.Core/Builder/*`.
- Tagging utilities or extensions: `src/Coven.Core/Tags/*`.
- Tests: `src/Coven.Core.Tests/*` with xUnit.

## Quick Checklist for a New Contribution

1) Add or update blocks and register them via the Builder.
2) If routing should prefer a block in certain scenarios, emit tags in upstream blocks and/or add capabilities to the candidate blocks.
3) Add focused unit tests demonstrating the new behavior (routing, tags, errors, async, etc.).
4) Build and run tests locally.
5) Keep changes minimal and consistent with existing patterns.

You’re set. If you want a concrete starting point, copy one of the test examples into a small console app, build a `MagikBuilder`, and iterate from there.

## Board Internals (Contributors)

The Board has been refactored to keep the public API and behavior unchanged while encapsulating routing complexity:
- `Routing/RegisteredBlock`: Immutable snapshot for each registry entry (types, name, merged capabilities, invoker, index).
- `Routing/BlockInvokerFactory`: Builds `Func<object, Task<object>>` invokers for blocks using expression trees.
- `Routing/DefaultSelectionStrategy`: Encapsulates selection order: `to:#<index>` → `to:<TypeName>` → capability overlap → registration order.
- `Routing/PipelineCompiler`: Owns the routing loop, tag emission (`by:<BlockTypeName>`), and path checks; compiles pipelines per `(startType, targetType)`.
- `Board`: Focuses on tag scoping, pipeline cache lookup, and optional precompilation. Public shape unchanged.

This separation keeps `Board` minimal and creates clean seams for future extensions (e.g., alternate selection strategies) without affecting public API or README content.
