# Unimplemented Features
- Licensing: Updated to BUSL‑1.1 with revenue gate and SPDX headers. Change License is MIT on 2029‑09‑11. NuGet packaging uses `<PackageLicenseFile>` and includes the root `LICENSE`.

- No input support
  - Need to overload magikblock so it has a no input version canonically.
  - Need to hide Empty from the world and remove it if we can.

- Type system breadth:
  - Support multi-input MagikBlocks with generic inputs `T, T2, ... T20` and dynamic generation of arity based on usage.
    - Key feature this enables is developer accessibility for complex typings like tuples.
  - Support variadic MagikUser with various books.

- Spellcasting library (Coven.Spellcasting):
  - Spellbook that automatically grabs all of the spells with their relevant documentation.
  - Testbook that automatically uses git delta to understand what tests map to which code regions.
  - Guidebook samples that preload full codebase into context.

- Samples:
  - Detailed sample app demonstrating multi-agent orchestration and use of Spellcasting constructs.
    - Needs to be our way of canonicalizing what the structure of the app looks like.

- Clarity improvements
  - Update published packages with correct repo url.
  - Update all packages with README.md metadata.
  - Ensure packages show up in github.
  - Public XML documents
  - Generate github pages for project
  - Consolidate testing into clear structures and paths that sell desired paths rather than possible paths.
  - Evaluate hiding public interfaces such that it's harder to misuse.
  - Detailed diagrams
  - Concept glyphs/colors
  - Autogenerate index

- Automagical distribution and reliability.
  - Distributed/multi-box execution (explicitly out-of-scope for now; track as future work).
  - Honor timeouts and retries for work dispatch in both modes.

- Observability and dashboards

- Sandboxing for custom agents

# Answer these questions

- Canonical API surface: Spellcasting docs vs code mismatch (e.g., `DefaultGuide/DefaultSpell/DefaultTest` and DI-based `MagikUser` vs current `Guidebook/Spellbook/Testbook` and `MagikUser<TIn,TOut,TGuide,TSpell,TTest>`). Which is canonical and what’s the migration plan?
- Codex CLI agent I/O: Are `ReadMessage`/`SendMessage` intended to be implemented (bidirectional), or is the agent strictly a rollout-to-`IScrivener` bridge? What’s the interaction model and lifecycle for `RegisterSpells` and MCP tool exposure?
- MCP shim/config: Who owns merging into `CODEX_HOME/config.toml` and how do we avoid server-name collisions and handle multiple concurrent sessions? When should callers pass `shimExecutablePath`, and how is cleanup handled?
- Pull mode contract: What are the guarantees for `GetWork`, branch IDs, tag persistence rules, timeouts/retries, and selection strategy overrides? How does this map to the promised “timeout/retry control” in docs?
- Licensing plan: README states MIT for < $100M and commercial at ≥ $100M, while TODO suggests BUSL 1.1 with delayed MIT. What’s the authoritative license, migration steps (packages, headers), and timeline for adopters?
