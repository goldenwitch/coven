# Unimplemented Features (from README)

- MagikBuilder enhancements:
  - Support all three add patterns: factory `(builder => return m)`, block instance, and inline lambda `(T => T2)`.
  - Auto-tagging of outgoing messages per block; optional override lambda to fully control emitted tags (static function constraint).

- Tags and routing:
  - Introduce a `Tag` model and attach tags to board postings; propagate tags through pipelines.
  - Implement `MagikTrick(...)` to split output to multiple downstream handlers based on best tag match; break ties by registration order.
  - Cycle detection/guardrails when overriding tagging can cause routing loops.

- Board features:
  - Implement Pull mode (`GetWork`) semantics: requests include the calling block and desired tags; lock/index behavior to answer deterministically.
  - Expose board configuration via builder `.Done(...)` or similar: global tag matching sort, timeout and retry handling, and Push vs Pull selection.
  - Honor timeouts and retries for work dispatch in both modes.
  - Implement `WorkSupported<T>(tags)` to reflect actual support based on registry and tag/routing rules (not a stub returning true).

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

- Samples and extensions:
  - Provide minimal sample apps demonstrating multi-agent orchestration and use of Spellcasting constructs.
  - Library of extensions to the core engine called out in README.

- Nice-to-have/future (called out in README text):
  - Distributed/multi-box execution (explicitly out-of-scope for now; track as future work).
