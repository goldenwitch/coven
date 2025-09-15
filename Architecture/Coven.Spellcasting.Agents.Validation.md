
# Coven.Spellcasting.Agents.Validation

> **Purpose.** Provide a tiny, idempotent **AgentValidation** extension point that can run as a regular **MagikBlock** (traditional compute) or be called manually anywhere in the pipeline. This leaves **MagikUser** reserved for *agent runtime*, while validation handles *environment readiness* (e.g., installing CLIs, checking versions) with a minimal public surface.

---

## Goals & Principles

- **MagikBlock, not MagikUser.** Validation is configuration/compute; it composes as a normal block.
- **Idempotent by design.** Safe to run multiple times.
- **Small public API.** Only expose what app/agent authors must implement or call.
- **Moveable.** Works before building, inside a ritual, or after `builder.Done()` via a manual call—no behavior change.

> **Depends on existing types** from `Coven.Spellcasting.Agents`: `AgentPermissions`, and the `RunCommand` action symbol.

---

## Using With Codex

- Codex auto-registers `IAgentValidation` when you call `AddCodexCliAgent(...)`.
- Recommended pattern: add `ValidateAgentBlock` before the block that invokes Codex. Codex will not function correctly if validation fails (e.g., missing CLI, unwritable workspace, unavailable named pipes, or missing shim when tools are present).
- Shim discovery is restrictive: only known shim filenames under `mcp-shim/` are considered; provide an explicit path if your packaging differs.
