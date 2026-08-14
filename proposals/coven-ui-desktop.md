# Coven UI Desktop

> **Status**: Draft  
> **Created**: 2026-07-29

---

## Summary

A cross-platform Avalonia 11 desktop application for Coven: chat with an agent, switch models and providers at runtime, and watch entries move through the journals live.

Two new packages plus one application:

| Project | Role |
|---------|------|
| `Coven.Chat.Ui` | Chat leaf bridging `ChatEntry` to an in-process UI channel |
| `Coven.Ui.Shell` | Journal for app-level concerns — reasoning, notices, model changes |
| `src/apps/Coven.Ui.Desktop` | Avalonia application; thin over the two above |

The application holds no orchestration logic. Everything it does is a projection of a journal.

---

## Motivation

Coven has no graphical surface. Chat leaves are [Console](../src/Coven.Chat.Console/README.md) and [Discord](../src/Coven.Chat.Discord/README.md); there is no HTTP, browser, or windowed client anywhere in the repo.

Beyond filling that gap, a desktop app is the best available demonstration of what makes Coven different. Append-only typed journals mean a chat UI is a *view over a log* rather than a pile of mutable state — which yields streaming, replay, and live introspection from one mechanism. The journal inspector in particular does something no conventional LLM client can: show the actual entry flow, positions and all, as a covenant executes.

---

## Design

### The ritual is the application lifetime

`Ritual<Empty, Empty>` runs until shutdown, exactly as in the console toys. Journals are **scoped** to the ritual, and `CovenExecutionScope.CurrentProvider` is `internal`, so the only supported route to them is constructor injection into a block.

```
Avalonia startup
  │
  ├─▶ build host for current provider selection
  ├─▶ hydrate journals from disk
  ├─▶ start Ritual<Empty,Empty> on a background task
  │      │
  │      └─▶ UiHostBlock : IMagikBlock<Empty,Empty>
  │             receives scoped journals by injection
  │             publishes handles to view models
  │             awaits application shutdown
  │
  └─▶ view models tail journals ──▶ render
```

This mirrors the `StartDaemonsBlock` pattern in the [FileScrivener README](../src/Coven.Scriveners.FileScrivener/README.md) and keeps the app inside the framework's grain rather than reaching around it.

### Coven.Chat.Ui leaf

Follows the six-part leaf shape used by [Coven.Chat.Console](../src/Coven.Chat.Console/README.md): config, gateway connection, session and factory, scrivener, transmuter, daemon, plus `AddUiChat()` and `UseUiChat()`.

The "gateway" is an in-process channel to the view models rather than a network connection — the only structural difference from any other leaf.

| Direction | Entries |
|-----------|---------|
| Produces | `ChatAfferent` — user input |
| Consumes | `ChatEfferent` — finalized message; `ChatChunk` — streaming fragment |

Consuming `ChatChunk` **and** `ChatEfferent` is deliberate. Chunks render tokens as they arrive; the windowed `ChatEfferent` is the finalized record. Waiting only for `ChatEfferent` would make streaming invisible, which is the entire point of the windowing layer.

### Coven.Ui.Shell journal

A manifest carries exactly one `JournalEntryType`, so app-level concerns need a second journal. This also keeps the chat transcript clean.

| Entry | Purpose |
|-------|---------|
| `UiThought` | Reasoning content for the reasoning pane |
| `UiNotice` | Model changed, provider switched, daemon failed, covenant rejected |

`AgentThought` routes to `UiThought` instead of being marked `Terminal<>`. Every current sample discards thoughts; here they already flow and simply need somewhere to land.

Recording model and provider changes as `UiNotice` preserves the audit trail without adding types to any agent manifest's `Produces` — see [Agent Provider Switching](agent-provider-switching.md).

### Panes

```
┌────────────────────────────────────────────┬──────────────────────┐
│  provider ▾   model ▾   ⟳      ● running   │  Journal Inspector   │
├────────────────────────────────────────────┤                      │
│                                            │  pos  journal  type  │
│  conversation                              │  ───────────────────  │
│  (ChatAfferent / ChatEfferent / ChatChunk) │  412  Chat    Affer. │
│                                            │  413  Agent   Prompt │
│                                            │  414  Agent   Chunk  │
├────────────────────────────────────────────┤  415  Agent   Chunk  │
│  ▸ Reasoning            (UiThought)        │  416  Chat    Chunk  │
├────────────────────────────────────────────┼──────────────────────┤
│  > input, or /command                      │  Window policy       │
│                                            │  paragraph  ☑        │
│                                            │  max len ──●──  2048 │
└────────────────────────────────────────────┴──────────────────────┘
```

**Journal inspector.** DI cannot enumerate registered scrivener types, so the inspected set is declared explicitly — `ChatEntry`, `AgentEntry`, `UiEntry`, `DaemonEvent` — and each is injected into `UiHostBlock`. `DaemonEvent` drives the status indicator: `StatusChanged` and `FailureOccurred` are already journaled by every [`ContractDaemon`](../src/Coven.Core/Daemonology/ContractDaemon.cs), so daemon health needs no new plumbing.

**Reasoning pane.** Collapsible, fed by `UiThought`. Extended thinking is already surfaced as thought chunks by the Claude leaf when enabled.

**Live window-policy tuning.** Window policies are registered scoped, so sliders cannot replace registrations without a rebuild. Instead a policy reads its thresholds from a shared settings object at decision time, composed with the existing paragraph policy via `CompositeWindowPolicy`. Moving a slider changes streaming cadence on the next chunk, with no teardown — the clearest available demonstration of semantic windowing.

### Model and provider switching

The picker groups by provider, orders newest-first, and shows inferred capability chips, all from [Agent Model Catalog](agent-model-catalog.md). Settings persist a **preferred family**, not a resolved model ID, so a new release is adopted on next launch.

| Action | Cost |
|--------|------|
| Change model within provider | Next request; config mutation |
| Change provider or API key | Session rebuild, roughly sub-second, masked by a progress state |

Rebuild mechanics and the hot/cold config split live in [Agent Provider Switching](agent-provider-switching.md).

Invalid combinations must surface as readable text, not stack traces. `CovenantValidationException` already carries actionable messages, and some combinations are rejected outright — `EnableStreaming()` with `EnableTools()` throws for Claude ([line 54](../src/Coven.Agents.Claude/ClaudeAgentsServiceCollectionExtensions.cs)), which the UI must present as a disabled toggle with an explanation rather than a crash. [Claude Streaming Tool Calling](claude-streaming-tool-calling.md) removes that particular restriction.

### Chat and CLI

Both readings of "chat/CLI" are cheap here.

| Surface | Mechanism |
|---------|-----------|
| Slash commands in the input box | `/model`, `/provider`, `/policy`, `/journal`, `/clear`, `/refresh` — parsed before the text becomes a `ChatAfferent` |
| Headless terminal mode | `--headless` builds the same covenant against the existing Console leaf |

Headless mode is nearly free: the covenant is identical, only the chat leaf differs. It also gives the app a scriptable form for testing.

### UI thread and backpressure

Session pumps run on background tasks; Avalonia collections must be mutated on the UI thread. Two concerns follow, and the second is easy to get wrong:

- Marshal journal notifications through the dispatcher.
- **Coalesce.** A per-token `ChatChunk` stream can produce thousands of dispatcher posts per response, and posting each one starves the UI. Chunks are batched per frame before append.

Render coalescing is the same problem the windowing layer already solves, applied to a different consumer.

### Secrets

API keys must not sit in plaintext config. .NET has no unified cross-platform secret API, so an `ISecretStore` abstraction is needed with per-OS backing: DPAPI on Windows, Keychain on macOS, libsecret on Linux, and — where none is available — an encrypted file that is **labeled as weaker in the UI** rather than presented as equivalent.

Existing environment variables (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `FS_ROOT`) are honored so the app works on first run like the toys.

---

## Milestones

| # | Deliverable | Depends on |
|---|-------------|------------|
| M0 | Journal hydration, pump start position, `IModelCatalog` + Claude implementation | — |
| M1 | `Coven.Chat.Ui`, `Coven.Ui.Shell`, Avalonia shell, chat pane with streaming, single provider from env | M0 |
| M2 | Catalogs for all four providers, model picker, in-provider switch, `CovenSession` rebuild, `ISecretStore` | M1 |
| M3 | Journal inspector, daemon status, reasoning pane | M1 |
| M4 | Live window-policy tuning, slash commands, `--headless` | M2, M3 |
| M5 | Claude streaming with tools, packaging and distribution | M4 |
| M6 | VINE plan and build modes — see [VINE Branch](vine-branch.md) | M3, M5 |

M0 lands entirely upstream and is testable with the existing harness before any UI exists.

M6 is separable and can be developed in parallel from M3 onward: `Coven.Vine` is a pure library with a ready-made conformance corpus and needs no UI to test.

---

## Open Questions

- **Repo-wide `TreatWarningsAsErrors`.** Avalonia's XAML compiler and MVVM source generators may emit warnings that fail the build; scoped `NoWarn` may be required in the app project only.
- **CI has no display.** [ci.yml](../.github/workflows/ci.yml) runs on `ubuntu-latest`, so tests stay at view-model level or use Avalonia's headless platform. No UI test may require a display.
- **Project location.** `src/apps/` is proposed; the app is neither a sample nor a toy. Must be `IsPackable=false`, and new Avalonia package versions go in [Directory.Packages.props](../Directory.Packages.props).
- **Journal growth.** `InMemoryScrivener` retains every entry for the process lifetime and NDJSON files never rotate. Long-lived sessions need the bounded-tail hydration described in [Journal Hydration](journal-hydration.md).
- **Token and cost telemetry.** Deliberately excluded: agent entries carry no usage data, so this needs upstream entry changes before a UI can show it.

---

## Scope

**In scope:**
- `Coven.Chat.Ui` leaf and `Coven.Ui.Shell` journal with READMEs
- Avalonia application: chat, reasoning, inspector, policy tuning
- Model and provider switching UI over the catalog and session abstractions
- Slash commands and `--headless` console mode
- `ISecretStore` with per-OS backing
- View-model tests, and E2E coverage of the UI leaf via the existing harness

**Out of scope:**
- Tool-call approval gate — a route filter plus a prompt; deferred, and required by [VINE](vine-branch.md) build mode
- Multi-conversation and tabbed sessions
- Token and cost telemetry
- Discord or other leaves surfaced in the desktop UI
- Theming and localization beyond Avalonia's Fluent default
- Auto-update and installer signing

---

## Dependencies

- [Journal Hydration](journal-hydration.md) — required for persistence and provider switching
- [Agent Provider Switching](agent-provider-switching.md) — session lifecycle
- [Agent Model Catalog](agent-model-catalog.md) — model discovery
- [Claude Streaming Tool Calling](claude-streaming-tool-calling.md) — required only for tools with streaming (M5)
- [VINE Branch](vine-branch.md) — project planning and agent-driven execution (M6)
- `Coven.Chat` windowing, `Coven.Core.Streaming` policies, `Coven.Testing.Harness` (implemented)

---

## Checklist

- [ ] `Coven.Chat.Ui`: config, gateway, session and factory, scrivener, transmuter, daemon
- [ ] `AddUiChat()` and `UseUiChat()` returning a manifest
- [ ] `Coven.Ui.Shell`: `UiEntry`, `UiThought`, `UiNotice`, manifest
- [ ] `UiHostBlock` publishing scoped journals to view models
- [ ] Avalonia shell, Fluent theme, MVVM wiring
- [ ] Chat pane with `ChatChunk` streaming and per-frame coalescing
- [ ] Reasoning pane fed by `UiThought`
- [ ] Journal inspector across `ChatEntry`, `AgentEntry`, `UiEntry`, `DaemonEvent`
- [ ] Daemon status indicator from `StatusChanged` / `FailureOccurred`
- [ ] Model picker: grouping, newest-first, capability chips, refresh
- [ ] Preferred-family persistence
- [ ] Provider switch with progress state
- [ ] Tunable window policy bound to sliders
- [ ] Slash command parser
- [ ] `--headless` mode over the Console leaf
- [ ] `ISecretStore`: DPAPI, Keychain, libsecret, labeled fallback
- [ ] Covenant validation errors rendered as readable text
- [ ] Solution entries, `IsPackable=false`, Avalonia versions centralized
- [ ] Test: streaming chunks render incrementally
- [ ] Test: model switch mid-conversation reaches the wire
- [ ] Test: provider switch preserves conversation
- [ ] Test: slash commands do not reach the chat journal
- [ ] `INDEX.md` entries for both new packages and the app
