# Unimplemented Features
- Fix licensing scheme to ensure reasonable compliance.
  - Update projects with busl1.1 licensing
  - Replace MIT with busl1.1
  - MIT in 4 years.

- Type system breadth:
  - Support multi-input MagikBlocks with generic inputs `T, T2, ... T20` and dynamic generation of arity based on usage.
    - Key feature this enables is developer accessibility for complex typings like tuples.

- Spellcasting library (Coven.Spellcasting):
  - Custom spellbook that automatically loads MCP as a tool.
  - Custom testbook that automatically uses git delta to understand what tests map to which code regions.
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
  - Hide public interfaces such that it's harder to misuse.
  - Detailed diagrams
  - Concept glyphs/colors

- Automagical distribution and reliability.
  - Distributed/multi-box execution (explicitly out-of-scope for now; track as future work).
  - Honor timeouts and retries for work dispatch in both modes.

- Observability and dashboards