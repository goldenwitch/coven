# Documentation

## Code References Over Duplication

- Reference actual code rather than duplicating it in markdown.
- If something doesn't have a working example in code, it doesn't belong in docs.
- Code examples in READMEs should be excerpts from real files, not standalone snippets that can drift.

## Present Tense Only

- Documentation describes what **exists**, not what **will exist**.
- No "planned", "future", "upcoming", "TODO", or roadmap language.
- If a feature isn't implemented, it isn't documented. Write a proposal instead.

## Architecture Files

- Architecture docs describe the current system.
- When the system changes, the architecture docs change in the same PR.
- Aspirational architecture belongs in `proposals/`, not `architecture/`.

## XML Doc Comments

- Public types and members require `<summary>` tags.
- Document behavior, not implementation.
- If you can't explain what it does in one sentence, reconsider the design.

## READMEs

- Every non-test project has a README.
- README describes: what it is, how to use it, prerequisites.
- Samples and toys: include how to run (`dotnet run`, required config).
