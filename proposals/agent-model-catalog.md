# Agent Model Catalog

> **Status**: Draft  
> **Created**: 2026-07-29

---

## Summary

Discover available models per provider at runtime instead of hardcoding identifiers. Adds `IModelCatalog` to `Coven.Agents`, implemented by each agent leaf, with a disk cache and family-pattern capability inference.

Goal: a new model release becomes selectable **without a code change**.

---

## Motivation

Every entry point in the repo hardcodes a model string — `"claude-sonnet-4-20250514"` in [sample 02](../src/samples/02.ConsoleClaudeFileSystem/Program.cs) and the [Claude toy](../src/toys/Coven.Toys.ConsoleClaude/Program.cs), `"gpt-5-2025-08-07"` in the root README, `"gemini-2.0-flash"` in the [Gemini client README](../src/Coven.Gemini.Client/README.md).

Hardcoded defaults rot in a specific and costly direction: they silently pin users to superseded models. An application that ships a constant is wrong the day the next model lands.

### Discovery is two problems, not one

This distinction drives the whole design.

| Question | Answerable from provider APIs? |
|----------|-------------------------------|
| Which models exist? | **Yes** — all three hosted providers list models |
| What can each model do? | **Mostly no** — only Gemini reports anything useful |

Listing models is solved. Capabilities are not: streaming support, tool calling, extended thinking, and vision are largely absent from list responses. A capability table keyed on exact model IDs would need editing per release — reintroducing exactly the maintenance burden this proposal removes.

---

## Design

### Listing

| Provider | Call | Chat-model signal |
|----------|------|-------------------|
| Anthropic | `GET /v1/models` (`x-api-key`, `anthropic-version`) | All returned models are chat models |
| OpenAI | `GET /v1/models` | None — response mixes embeddings, audio, image, moderation |
| Gemini | `GET /v1beta/models` | `supportedGenerationMethods` contains `generateContent` |
| LLamaSharp | Scan configured directory | `*.gguf` files on disk |

Anthropic and OpenAI both return a creation timestamp; Gemini does not. Both hosted list endpoints paginate. Exact field names must be verified against current provider documentation at implementation time — this table records the shape, not a contract.

The Gemini leaf already builds raw REST paths against `/v1beta/models/{model}` in [`GeminiRestClient`](../src/Coven.Gemini.Client/GeminiRestClient.cs) and normalizes the `models/` prefix, so listing is a small addition to an existing client rather than new infrastructure.

### Descriptor

```
STRUCTURE ModelDescriptor
  Id             -- provider-native identifier sent on the wire
  DisplayName    -- human label; falls back to Id
  Family         -- inferred group key, e.g. "claude-sonnet", "gpt", "gemini-pro"
  Created        -- optional; drives newest-first ordering
  ContextWindow  -- optional; only Gemini reports it
  Capabilities   -- inferred, not authoritative
```

`ModelDescriptor` is a plain type, not an `Entry`. Journals earn their cost for streams that need audit and replay; a cached catalog lookup is a query. Keeping it out of the journal avoids adding types to any manifest's `Produces` and leaves existing covenant validation untouched.

`IModelCatalog` belongs in `Coven.Agents` — the branch abstraction leaves implement — mirroring how `ToolDefinition` is defined there and consumed by leaves. The interface adds no HTTP dependency to `Coven.Agents`; implementations live beside their gateways.

### Capability inference by family, not by identifier

Rules match on **family prefix**, so unreleased models inherit their family's rules automatically.

```
RULES (ordered, first match wins)
  "claude-*-opus-*"   | "claude-opus-*"    -> streaming, tools, thinking, vision
  "claude-*-sonnet-*" | "claude-sonnet-*"  -> streaming, tools, thinking, vision
  "claude-*-haiku-*"  | "claude-haiku-*"   -> streaming, tools, vision
  "gpt-*" | "o[0-9]*" | "chatgpt-*"        -> streaming, tools, vision
  "gemini-*-pro*"     | "gemini-*-flash*"  -> streaming, tools, vision
  *.gguf                                    -> streaming
  UNMATCHED                                 -> streaming, tools; family "other"
```

Three properties follow, and each matters:

- **A new model in a known family appears automatically.** `gpt-6-mini` matches `gpt-*` with no edit.
- **A new family is still visible.** Unmatched models are grouped as `other` with conservative capabilities rather than hidden. Silent omission would be the worse failure — a user could not select a model the app declined to show.
- **Rules are data, not code.** A user-editable overrides file corrects a misclassification without a rebuild.

Inferred capabilities are advisory. They shape the UI — which toggles to enable, which chips to show — and must never be the only guard. The provider's own error is authoritative; a 400 for an unsupported parameter is the real answer and should surface as a readable message.

### Filtering non-chat models

OpenAI's list requires filtering, and the direction matters. An **allowlist of family prefixes** is used rather than a denylist of known non-chat models: a denylist fails open, letting a new embedding model appear as a chat option, and needs editing per release. An allowlist fails closed into the visible `other` group.

### Caching and offline behavior

```
PROCEDURE GetModels(provider, ct)
  IF cache fresh (within TTL) AND not force-refresh:
    RETURN cached

  TRY
    models = fetch from provider
    write cache to disk
    RETURN models
  CATCH network or auth failure
    IF cache exists (any age):
      RETURN cached, flagged stale
    ELSE
      RETURN empty, surface the error
```

The cache is what makes this usable: the picker opens instantly, works offline, and functions before a key is entered. A stale cache is marked in the UI, never silently presented as current. Default TTL of 24 hours, with explicit refresh.

### Default selection

This is the mechanism that actually keeps users off outdated models, and it is more important than the listing itself.

```
PROCEDURE ResolveDefault(provider, preferredFamily)
  candidates = catalog(provider) WHERE family == preferredFamily
  IF candidates non-empty:
    RETURN newest by Created, else highest version by natural sort
  RETURN newest across all families
```

Applications persist a **preferred family** (`claude-sonnet`) rather than a model ID. The concrete model is resolved at startup from the live catalog. A new Sonnet release is picked up on next launch with no user action and no code change — a hardcoded ID cannot do this, and neither can persisting a resolved ID.

Where a hardcoded fallback is unavoidable — no cache, no network, first run — it is a last resort behind both the catalog and the cache, and should be logged as a fallback so its use is visible rather than silent.

---

## Scope

**In scope:**
- `IModelCatalog` and `ModelDescriptor` in `Coven.Agents`
- Implementations for Claude, OpenAI, Gemini, LLamaSharp (directory scan)
- Pagination for hosted providers
- Family-pattern capability rules with a user-editable overrides file
- Allowlist filtering of non-chat models
- Disk cache with TTL, explicit refresh, stale flagging, offline fallback
- Preferred-family default resolution
- Virtual catalog in `Coven.Testing.Harness` for scripted model lists

**Out of scope:**
- Pricing and token-cost data — not in list responses; needs a separate source
- Rate-limit and quota discovery
- Capability probing by trial request
- Per-model parameter validation (max `MaxTokens`, valid temperature range)
- Automatic model switching on deprecation notice

---

## Dependencies

- Existing gateway HTTP setup per leaf (implemented)
- [`GeminiRestClient`](../src/Coven.Gemini.Client/GeminiRestClient.cs) path building and model-ID normalization (implemented)
- Consumed by [Coven UI Desktop](coven-ui-desktop.md) and [Agent Provider Switching](agent-provider-switching.md)

---

## Checklist

- [ ] `ModelDescriptor` and `IModelCatalog` in `Coven.Agents`
- [ ] `ClaudeModelCatalog` — `/v1/models` with pagination
- [ ] `OpenAIModelCatalog` — `/v1/models` with allowlist filtering
- [ ] `GeminiModelCatalog` — `/v1beta/models`, filter on `generateContent`
- [ ] `LLamaSharpModelCatalog` — `*.gguf` directory scan
- [ ] Family-pattern rule engine with ordered first-match
- [ ] User-editable overrides file
- [ ] Disk cache: TTL, refresh, stale flag, offline fallback
- [ ] Preferred-family default resolution
- [ ] `VirtualModelCatalog` for scripted lists in tests
- [ ] Test: unknown model ID lands in `other` and stays selectable
- [ ] Test: stale cache returned and flagged when fetch fails
- [ ] Test: preferred family resolves to newest by `Created`
- [ ] Test: OpenAI allowlist excludes embedding and audio models
- [ ] Replace hardcoded model constants in samples and toys with family preferences
