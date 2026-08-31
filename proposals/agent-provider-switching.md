# Agent Provider Switching

> **Status**: Draft  
> **Created**: 2026-07-29

---

## Summary

Let an application change which agent leaf serves prompts at runtime. Two distinct operations with very different costs:

| Operation | Example | Mechanism | Cost |
|-----------|---------|-----------|------|
| **Model switch** (same provider) | Sonnet → Opus | Mutate the shared config instance | Next request; no teardown |
| **Provider switch** | Claude → OpenAI | Rebuild the host scope | Sub-second rebuild |

Introduces a `CovenSession` lifecycle owning one host per provider selection, and an explicit rule for which config fields are hot-mutable.

---

## Motivation

Every agent leaf registers identically:

- `services.AddScoped(_ => config)` — one shared, mutable config instance
- `services.TryAddScoped<IScrivener<AgentEntry>, InMemoryScrivener<AgentEntry>>()` — first registration wins
- `services.AddScoped<ContractDaemon, XxxAgentDaemon>()` — **additive**

See [Claude](../src/Coven.Agents.Claude/ClaudeAgentsServiceCollectionExtensions.cs), and OpenAI, Gemini, and LLamaSharp identically.

The additive daemon registration is the blocker. Registering two providers in one coven yields two agent daemons tailing the same `AgentEntry` journal, both consuming every `AgentPrompt`:

```
                    ┌──────────────────────┐
   AgentPrompt ────▶│ IScrivener<AgentEntry>│
                    └───────┬───────┬──────┘
                            │       │
             ClaudeAgentDaemon   OpenAIAgentDaemon
                            │       │
                            ▼       ▼
                    two AgentResponse entries for one prompt
```

There is no keying, no filtering, and no ownership concept. Multi-provider co-registration is not merely unsupported — it silently double-bills and double-answers.

---

## Design

### Session rebuild is with the grain

Coven validates covenants at build time on purpose. Provider identity is part of the covenant: the manifest's `Produces` and `Consumes` sets differ per provider and per registration flag — [`UseClaudeAgents`](../src/Coven.Agents.Claude/ClaudeCovenBuilderExtensions.cs) adds `AgentToolCall` only when `EnableTools()` is set.

A different provider is therefore a different covenant, and rebuilding is the correct response rather than a workaround. The alternative — one journal multiplexed across providers at runtime — moves provider selection past the validator into untyped dispatch, trading away the guarantee the framework exists to provide.

### CovenSession

```
STRUCTURE CovenSession
  host:        IHost                -- built for one provider selection
  ritualTask:  Task                 -- long-running Ritual<Empty, Empty>
  journals:    handles published by the host block

PROCEDURE SwitchProvider(target, settings)
  drain     current session         -- await in-flight turn, or cancel
  flush     journals to disk        -- FlusherDaemon shutdown flush
  dispose   current session         -- EndScopeAsync stops daemons in reverse
  build     new host for target
  hydrate   journals from disk      -- see Journal Hydration
  start     new session
```

The ritual is the session's lifetime. As in the console toys, `Ritual<Empty, Empty>` runs until shutdown; a host block receives the scope's journals by constructor injection and publishes them to the application. `CovenExecutionScope.CurrentProvider` is `internal`, so block injection is the only supported route to scope-resident journals.

Conversation continuity across a switch depends entirely on [Journal Hydration](journal-hydration.md) — without it, a provider switch loses the conversation.

### Hot-mutable configuration

`AddScoped(_ => config)` closes over a single instance, and gateways read fields when building each request — [`ClaudeRequestGatewayConnection`](../src/Coven.Agents.Claude/ClaudeRequestGatewayConnection.cs) reads `_configuration.Model` per request and transmutes `_configuration` into request options per send. Mutating those fields takes effect on the next turn.

Fields captured at construction are **not** hot-mutable. The same gateway builds its `HttpClient` once, writing `ApiKey` into a default header.

| Field | Hot | Why |
|-------|-----|-----|
| `Model` | ✅ | Read when building each request |
| `MaxTokens`, `Temperature`, `TopP`, `TopK` | ✅ | Transmuted from config per send |
| `SystemPrompt`, `StopSequences`, `HistoryClip` | ✅ | Read per send |
| `ExtendedThinking` | ✅ | Transmuted per send |
| `ApiKey` | ❌ | Baked into `HttpClient` headers at construction |
| `Endpoint` | ❌ | Base address resolved at construction |
| Registration flags (`EnableStreaming`, `EnableTools`) | ❌ | Change the manifest; require rebuild |

Each leaf must document its own hot/cold split. Relying on this without documenting it makes an implementation detail load-bearing, so the split belongs in each leaf's README and should be covered by a test that asserts a mid-session model change reaches the wire.

### Auditability

A hot mutation bypasses the journal, which sits badly with *"journal or it didn't happen."* The switch should be recorded — but writing a new `AgentEntry` subtype would add to `Produces` and force every existing covenant to route it, breaking validation for current samples.

Recording the change in the application's own journal instead (`UiEntry` in [Coven UI Desktop](coven-ui-desktop.md)) keeps agent covenants untouched while preserving the audit trail. Journal-per-concern is what makes this cheap.

### Rejected alternatives

**Keyed per-provider journals.** Give each leaf its own keyed `IScrivener<AgentEntry>` and route `AgentPrompt` by predicate — the covenant already supports route filters. Enables mid-conversation switching with no teardown, but every leaf must be reworked to bind keyed journals, and N providers means N idle daemons and N transcript builders resident.

**Gateway multiplexing.** One daemon and journal, with a facade selecting a provider per request. Best UX, largest refactor: each leaf has provider-specific internal entries (`ClaudeEntry`, `OpenAIEntry`), its own transcript builder, and its own streaming shape. Worth revisiting once more than two providers are routinely used together.

Both remain open; neither is needed for a working application.

---

## Scope

**In scope:**
- `CovenSession` lifecycle: build, start, drain, dispose, rebuild
- Provider switch via session rebuild, with journal flush and hydration across the boundary
- Hot config mutation for model and sampling parameters
- Per-leaf documentation of the hot/cold field split
- Guard rejecting co-registration of two agent leaves with an actionable message

**Out of scope:**
- Keyed per-provider journals
- Gateway-level multiplexing
- Concurrent multi-provider execution (fan-out to several models at once)
- Changing how leaves register daemons

---

## Dependencies

- [Journal Hydration](journal-hydration.md) — required for conversation continuity across a switch
- [Agent Model Catalog](agent-model-catalog.md) — supplies the model list a switch selects from
- `CovenExecutionScope` scope lifecycle (implemented)

---

## Checklist

- [ ] `CovenSession` abstraction owning host, ritual task, and journal handles
- [ ] Drain semantics: await in-flight turn or cancel on demand
- [ ] Provider switch: flush → dispose → build → hydrate → start
- [ ] Host block publishing scope journals to the application
- [ ] Guard: two agent leaves in one coven fails at build time, not at runtime
- [ ] Hot/cold field table in each agent leaf README
- [ ] Test: model changed mid-session reaches the wire on the next request
- [ ] Test: API key change requires rebuild (documents the cold path)
- [ ] Test: provider switch preserves conversation via hydration
- [ ] Test: co-registration guard produces an actionable error
