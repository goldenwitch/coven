# Coven.Spellcasting

> Provide three canonical “books” (Guide/Spell/Test) to agent code automatically, keeping the system **unopinionated**, **code‑first**, and **type‑safe**. Agents remain user‑owned; no external config files or orchestration in this layer. This doc reflects the simplified design **without composite factories**.

---

## Goals

- Treat **MagikUser** as a first‑class `IMagikBlock<TIn,TOut>` that users inherit to write their own agent logic.
- Automatically build and pass **three canonical books** into `InvokeAsync`:
  - **Guidebook** — usually Markdown guidance, but fully generic.
  - **Spellbook** — typed structure describing recipes/instructions.
  - **Testbook** — typed structure describing scenarios/invariants.
- Keep the **public API tiny** and focused on what developers implement.
- Keep **factories optional**. Typical developers get defaults; advanced teams can inject their own factories and typed payloads.
---

## Design Notes & Trade‑offs

- **Alignment with Coven:** `DoMagik(TIn)` satisfies `IMagikBlock<TIn,TOut>`; `InvokeMagik` handles MagikUser specific context.

---

## Lifecycle & Guarantees

- **Execution:** `DoMagik` constructs Guide/Spell/Test then calls `InvokeMagik` with all three plus the original input.
- **Ownership:** This library does not define “the agent.” It is agnostic to transport (CLI/HTTP/RPC) and model family.
- **Typing:** Payloads are fully generic; teams can evolve schemas without changing the core surface.

---

## Agents Overview

An `ICovenAgent<TMessage>` is responsible for:
- Starting and controlling the agent runtime lifecycle.
- Registering spells (via `ISpellContract`) so tools are available to the agent.

For the Codex CLI implementation, see `Coven.Spellcasting.Agents.Codex`.
