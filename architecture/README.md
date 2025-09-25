# Architecture Guide

Explore by component. Standard patterns and integrations live in the root [README](../README.md). All docs here are flattened and named after their primary namespace for quick discovery.

## Standards

- Flat structure: Keep docs in this folder; avoid deep trees. Use a subfolder only for large bundles that cannot be reasonably flattened.
- Titles: H1 must map to the primary namespace (e.g., `Coven.Spellcasting`).
- Scope: Component descriptions only; do not include detailed code that already exists in the codebase. Include only usage snippets that users must write.
- Independence: Document components independently; wiring guides come later, once components stand alone.
- Size: Keep docs focused and readable. Target ~240 lines to reduce the chance of missing key details.
- Canonical patterns: If a pattern is required for all users, place it in the repo root [README](../README.md) (not here).
- Deprecations: When we delete or deprecate a feature, purge its docs immediately.
- Index hygiene: Update this README and the repo [INDEX](../INDEX.md) whenever docs change.
- All non-standard library dependencies must be isolated to integrations.
- Only integrations may depend on integrations.

## Cancellation Tokens

- Optional parameter: Use `CancellationToken cancellationToken = default` as the last parameter on async APIs where cancellation is meaningful; avoid overload explosions.
- Propagation: Always forward the token to downstream calls; do not use `CancellationToken.None`.
- Background services: Honor the provided `stoppingToken` and pass it to all awaited operations.
- Linked tokens: Only link when composing multiple sources or applying an internal upper bound; dispose the CTS.
- Exceptions: Treat `OperationCanceledException` as cooperative shutdown; don’t log it as an error or wrap it.
- I/O: Prefer token-aware APIs; if missing, use a cancel-aware pattern (avoid `WaitAsync` if a better token overload exists).

## Core Components
- [Coven.Core](Coven.Core.md): Core runtime (MagikBlocks, builder, routing, board, IScrivener, ITransmuter).
- [Coven.Chat](Coven.Chat.md): Patterns for enabling conversation from external sources (Console or Discord for example)
- [Coven.Daemonology](Coven.Daemonology.md): Describes core features for long running hosts.
- [Coven.Spellcasting](Coven.Spellcasting.md): Spell contracts and schema conventions. This is how we wire in tools.

## Integrations
- [Coven.Chat.Console](Coven.Chat.Console.md): Wires stdin/stdout into Coven as ChatEntry.
- [Coven.Chat.Discord](Coven.Chat.Discord.md): Wires discord messages into Coven as ChatEntry.
- [Coven.Codex](Coven.Codex.md): Implementation of Codex CLI Daemon.
- [Coven.OpenAI](Coven.OpenAI.md): Implementation of OpenAI responses API as a Coven-style Daemon.
- [Coven.Spellcasting.MCP](Coven.Spellcasting.MCP.md): Enables consumption of spells as MCP tools.

## Meta/Misc
- [Licensing](./Licensing.md)
