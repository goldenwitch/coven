# Refactor Topic Title

Scope: Briefly describe the refactor scope and boundaries. State whether back-compat is honored during the refactor.

Status: Current status (e.g., In Progress / Complete). Note tests/docs status.

## Conventions
- Tags:
  - [ok]: Reviewed and aligned
  - [bug]: Functional defect fixed
  - [api]: Public API change
  - [internal]: Internal behavior/implementation detail
  - [tests]: Test-only change
  - [docs]: Documentation work
  - [cleanup]: Code quality/non-functional cleanup
  - [redundant]: Unnecessary pattern removed/avoided
  - [design]: Open design question

## Principles (adopted)
- List the guiding principles for this refactor (what good looks like). Keep short and actionable.

## API Surface Changes
- Enumerate impacted public interfaces/classes/methods with new signatures or behaviors. Use [api] tags.

## Migration Checklist
- Concrete, ordered steps to bring code to the new standard. Keep it actionable and brief.

## Component Summary (post-refactor)
- Summarize by subsystem (Core, Agents, Samples/Toys, Tooling, etc.) with final state and any notable caveats.

## Reviews (by project)
- ProjectName
  - [tag] One-line finding or change. Repeat per file or concern as needed.

## Detailed Change Log
- Bullet list of applied changes with tags. Group related changes together for readability.

## Testing & Verification
- What was tested, how, and current result (e.g., “All tests pass”). Include any manual checks or audits.

## Deferred / Follow-ups
- Items intentionally left for later (with [docs]/[design]/[cleanup] tags as appropriate).

## Anti-Patterns Avoided
- Call out common pitfalls prevented by the refactor.

## References
- Link to root README / Architecture docs or other canonical guidance updated in this refactor.

