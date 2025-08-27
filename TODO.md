# Unimplemented Features (from README)

- MagikBuilder enhancements:
  - Support all three add patterns: factory `(builder => return m)`, block instance, and inline lambda `(T => T2)`.
  - Support DI registration and injection into the magikbuilders.

- Board features:
  - Honor timeouts and retries for work dispatch in both modes.

- Type system breadth:
  - Support multi-input MagikBlocks with generic inputs `T, T2, ... T20` and dynamic generation of arity based on usage.
  - Add a Roslyn analyzer to enforce MagikBlock inputs are immutable records.

- Spellcasting library (Coven.Spellcasting):
  - Add `MagikUser` template with configuration, Codex CLI wrapper, tool calls, agent definitions, and validation integration.
  - Implement `Spellbook` (tooling), `Testbook` (evaluations with when/where mappings), and `Guidebook` (role/context, startup info).
  - Samples that wrap MCP server tools via Spellbooks.
  - Testbook samples that derive tests from git deltas and code coverage.
  - Guidebook samples that preload full codebase into context.

- Connectivity adapters and samples:
  - Define an adapter interface mapping external message platforms to Coven.
  - Provide sample adapters: Console and Discord.

- Samples:
  - Provide minimal sample apps demonstrating multi-agent orchestration and use of Spellcasting constructs.
  - 
- Nice-to-have/future (called out in README text):
  - Distributed/multi-box execution (explicitly out-of-scope for now; track as future work).
