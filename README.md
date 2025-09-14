# Coven

**What it is.** A .NET engine for orchestrating multiple AI coding agents (“MagikBlocks”) that collaborate in a shared context, with routing decided at runtime by tags/capabilities. It builds on Codex CLI’s sandboxing and tooling and currently targets single‑machine use.

## Core ideas

**MagikBlock**: typed unit of work; compose into graphs with **MagikBuilder**; configuration becomes immutable at `.Done()`.
**Tagging & capabilities** drive selection. Routes to the node with the most matching tags, uses registration order as tie breaker.
**Board**: runtime that posts/consumes work; supports Push (recommended) and Pull modes with timeout/retry control.

## Quick Start (DI + Codex CLI)

Minimal wiring using dependency injection, matching the `samples/01.LocalCodexCLI` project. The Codex CLI agent streams Codex rollout into `ChatEntry` messages via a required translator.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.Rollout;

var services = new ServiceCollection();

// Register the Codex CLI agent for ChatEntry, using the default translator.
services.AddCodexCliAgent<ChatEntry, DefaultChatEntryTranslator>(o =>
{
    o.ExecutablePath = "codex"; // absolute path if not on PATH
    o.WorkspaceDirectory = Directory.GetCurrentDirectory();
    o.ShimExecutablePath = null; // provide if using MCP spells
});

// See the sample for full console wiring and Coven composition.
```

Note: Ensure `codex` is on your PATH or provide an absolute `ExecutablePath`. A translator is required; `DefaultChatEntryTranslator` is used for `ChatEntry`.

For a complete, runnable walkthrough, follow the `samples/01.LocalCodexCLI` project.

## Canonical Patterns

- Use `MagikUser<TIn,TOut>` to encapsulate agent logic. Provide Guidebook, Spellbook, and Testbook via DI or builders, then implement `InvokeMagik` to drive your agent.
- Define tools as spells implementing `ISpellContract` (`ISpell`, `ISpell<TIn>`, `ISpell<TIn,TOut>`). Register spells with agents by passing an `IReadOnlyList<ISpellContract>`.
- Prefer DI everywhere: resolve scriveners, agents, and blocks from the container; let `BuildCoven` compose graphs and finalize with `.Done()`.
- Keep agents unopinionated: external transport (CLI/HTTP/RPC) is owned by the agent package (e.g., Codex CLI). Your app wires them together.
- Schema generation is automatic: spell schemas derive from their generic types; Spellbooks are the source of truth for names/schemas passed to agents.

## Samples

Explore runnable examples in `/samples`. Open `samples/Coven.Samples.sln` to browse all samples side-by-side, or use each sample’s individual `.sln` in its folder.

## VS Code: Run/Debug

- Launch configs: Use the Run and Debug panel to select one of the provided startups:
  - `.NET Launch (Console) - 01.LocalCodexCLI` (under `samples/01.LocalCodexCLI`)
  - `.NET Launch (Console) - ConsoleEcho` (under `Toys/Coven.Toys.ConsoleEcho`)
- Input/output: Interact via the Terminal panel, not the Debug Console. Pick the terminal tab that matches the launch name.
- External console: Prefer a separate window? Change `console` to `externalTerminal` in `.vscode/launch.json` for that configuration.
- Codex note (01 sample): `01.LocalCodexCLI` expects `codex` on PATH; configure per sample README if needed.

# Appendix 

- [Code Index](/INDEX.md)
- [Architecture Guide](/Architecture/README.md)
- [Spellcasting (Design)](/Architecture/Coven.Spellcasting.md)

## License

Dual licensing:

- Community: Business Source License 1.1 (BUSL‑1.1) with an Additional Use Grant permitting Production Use if you and your affiliates made < US $100,000,000 in combined gross revenue in the prior fiscal year. See `LICENSE` for the precise terms and definitions.
- Commercial/Enterprise: Available only under a separate, mutually executed agreement with Licensor. See `COMMERCIAL-TERMS.md`.

SPDX: `BUSL-1.1`

- [Notice](/NOTICE)
- [License](/LICENSE)
- [Commercial Terms](/COMMERCIAL-TERMS.md)

[![Support on Patreon](https://img.shields.io/badge/Support-Patreon-e85b46?logo=patreon)](https://www.patreon.com/c/Goldenwitch)
