# Architecture Guide

Start with Overview, then explore by component. All docs here are flattened and named after their primary namespace for quick discovery.

## Standards

- Flat structure: Keep docs in this folder; avoid deep trees. Use a subfolder only for large bundles that cannot be reasonably flattened.
- Titles: H1 must map to the primary namespace (e.g., `Coven.Spellcasting`).
- Scope: Component descriptions only; do not include detailed code that already exists in the codebase. Include only usage snippets that users must write.
- Independence: Document components independently; wiring guides come later, once components stand alone.
- Size: Keep docs focused and readable. Target ~240 lines to reduce the chance of missing key details.
- Canonical patterns: If a pattern is required for all users, place it in the repo root `README.md` (not here).
- Deprecations: When we delete or deprecate a feature, purge its docs immediately.
- Index hygiene: Update this README and the repo `INDEX.md` whenever docs change.

## Components (overview)

- Coven.Core: Core runtime (MagikBlocks, builder, routing, board).
- Coven.Spellcasting: Agent-facing primitives (Guidebook, Spellbook, Testbook, MagikUser).
- Coven.Spellcasting.Spells: Spell contracts and schema conventions.
- Coven.Spellcasting.Agents.Codex: Codex CLI agent integration (MCP tools + rollout tailing).
- Coven.Spellcasting.Agents.Validation: Validates agent environment readiness.
- Coven.Chat: Journaling contracts (IScrivener) and chat message patterns.
- Coven.Analyzers: Roslyn analyzer pack for Coven architectural constraints.
- Tags & Routing: Tag model and selection behavior.
- Board: Push/Pull runtime and message flow.
- Dependency Injection: DI patterns used across the stack.

## Getting Started
- [Overview](./Overview.md): Architectural tour and orientation.

## Core Concepts
- [MagikBlocks & Builder](./MagikBlocks.md): Compose work as blocks; finalize immutable graphs with `.Done()`.
- [Tags & Routing](./TagsAndRouting.md): Route work dynamically by tags/capabilities.
- [MagikTrick (Fenced Routing)](./MagikTrick.md): Constrain routing with fences and scoped capabilities.

## Runtime
- [Board (Push & Pull)](./Board.md): Execution runtime with push/pull consumption, timeouts, and retries.
- [Dependency Injection](./DependencyInjection.md): DI patterns and lifetimes used across components.

## Spellcasting
- [Coven.Spellcasting](./Coven.Spellcasting.md): Agent-facing primitives (Guidebook, Spellbook, Testbook, MagikUser).
- [Coven.Spellcasting.Spells](./Coven.Spellcasting.Spells.md): Spell contracts, schema generation, and Spellbook source of truth.
- [Coven.Spellcasting.Agents.Codex](./Coven.Spellcasting.Agents.Codex.md): Codex CLI integration (MCP tools, rollout tailing).
- [Coven.Spellcasting.Agents.Validation](./Coven.Spellcasting.Agents.Validation.md): Validates environment readiness.

## Chat Subsystem
- [Coven.Chat](./Coven.Chat.md): Journaling contracts (IScrivener) and message patterns.

## Tooling
- [Coven.Analyzers](./Coven.Analyzers.md): Roslyn analyzers that enforce Coven architectural constraints.

## Meta
- [Contributing](./Contributing.md)
- [Licensing](./Licensing.md)
