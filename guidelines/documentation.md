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

## No Realistic Code in Architecture Docs

Code in markdown rots when the actual implementation changes. Architecture documents should describe **concepts**, not **implementation details**. Realistic-looking code creates false confidence that it's accurate.

### What's Banned

Literal C# code blocks that look like they could compile:
- Class definitions, record declarations, interface signatures
- Method implementations with real logic
- DI registrations (`services.AddSomething(...)`)
- Full working examples that duplicate actual source files

### What to Use Instead

| Instead of... | Use... |
|---------------|--------|
| Class/interface definitions | Diagrams (ASCII, Mermaid) or links to source files |
| Method implementations | Pseudocode (clearly marked, language-agnostic) |
| DI wiring examples | Prose describing the registration pattern |
| "Complete examples" | Links to `src/samples/` or `src/toys/` |
| Inline code snippets | Tables describing relationships and behaviors |

### Acceptable Code Forms

- **ASCII diagrams**: Flow and structure illustrations using box-drawing characters
- **Mermaid diagrams**: Rendered flowcharts and sequence diagrams
- **Pseudocode**: Clearly marked as pseudocode, not language-specific
- **Short inline references**: Type names in backticks (e.g., `IScrivener<T>`)
- **File links**: Direct links to actual source (e.g., `[ChatEntry.cs](../src/Coven.Chat/ChatEntry.cs)`)
- **Validation error examples**: Showing what the tooling rejects (not working code)

### Future: Live Code References

The markdown code reference system ([proposals/markdown-code-references.md](../proposals/markdown-code-references.md)) will enable embedding actual source code that stays synchronized with the implementation. Until then, prefer links over inline code.

### Enforcement

When reviewing architecture docs:
1. If a code block could compile, it should be a link to source instead
2. If it's illustrating a concept, convert to pseudocode or a diagram
3. If it's showing an error case, mark it clearly as "what not to do"

## XML Doc Comments

- Public types and members require `<summary>` tags.
- Document behavior, not implementation.
- If you can't explain what it does in one sentence, reconsider the design.

## READMEs

- Every non-test project has a README.
- README describes: what it is, how to use it, prerequisites.
- Samples and toys: include how to run (`dotnet run`, required config).
