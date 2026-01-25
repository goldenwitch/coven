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

## Proposals

Proposals are the **only** place for aspirational documentation. Ideas, future features, and design documents for unimplemented work belong in `proposals/`.

### What Goes Where

| Content Type | Location |
|--------------|----------|
| Ideas, designs for unimplemented features | `proposals/` |
| How the current system works | `architecture/` |
| How to use a specific component | Component `README.md` |

### Proposal Lifecycle

1. **Draft** → Initial idea, incomplete, seeking early feedback
2. **Proposal** → Complete design ready for review
3. **Accepted** → Approved for implementation
4. **Implemented** → Code is merged; proposal awaits integration
5. **Deleted** → Content integrated into architecture/READMEs; file removed

### Integration Requirement

When a proposal is implemented:
1. Integrate relevant content into `architecture/` docs and/or component READMEs
2. Delete the proposal file

Proposals are not permanent documentation. They are temporary artifacts that exist only while work is unimplemented.

### Status Format

Each proposal must include a status header:

```
Status: Draft | Proposal | Accepted | Implemented
```

Mark proposals as `Implemented` only after the code is merged. Delete them only after content is integrated elsewhere.
