# Toys: Coven.Toys.CodexConsole — Refactor Notes (Pass 2)

Scope: fresh scan after prior cleanup. New issues only.

- [bug] Fixed shim path: `AppContext.BaseDirectory/Coven.Spellcasting.Agents.Codex.McpShim.exe` may not exist on some RIDs or build layouts (DLL-only). If missing, validation fails; user must provide a correct path.
- [bug] Validation environment: Named pipe handshake can fail in restricted environments; ritual will error during `ValidateAgentBlock`.
- [redundant] Empty `.codex/` folder remains in the toy after artifact wipe; safe to delete entirely since agent recreates it at runtime.
