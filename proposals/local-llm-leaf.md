# Local OpenAI Leaf

> **Status**: Draft  
> **Created**: 2026-02-14  
> **Dependencies**: None

## Summary

A leaf adapter for integrating locally-hosted OpenAI-compatible language models with the Agents branch. Targets self-hosted inference servers exposing the OpenAI API contract, starting with `gpt-oss:20b` as the reference model.

## Problem

The existing `Coven.Agents.OpenAI` leaf targets cloud endpoints. Users running local OpenAI-compatible servers need to:

- Point to a local endpoint instead of `api.openai.com`
- Handle different timeout characteristics (local inference varies by hardware)
- Skip API key validation when running without authentication
- Avoid cloud-specific features (billing, usage tracking, organization IDs)

A dedicated local leaf provides these affordances without polluting the cloud leaf's configuration.

## Scope

**Breaking changes**: None. New leaf; existing systems unaffected.

**In scope:**
- Gateway connection to local OpenAI-compatible endpoints (`/v1/chat/completions`)
- Streaming response mode
- Entry types mirroring the Agents branch contract
- Transmutation between local and branch journals
- Configuration for endpoint, model, and generation parameters

**Out of scope:**
- Model management (downloading, loading, quantization)
- GPU/hardware orchestration
- Non-OpenAI-compatible protocols
- Prompt templating (handled by branch-level transmuters)

## Design

### Entry Types

The leaf defines entries parallel to the cloud OpenAI leaf:

| Entry | Direction | Purpose |
|-------|-----------|---------|
| `LocalOpenAIEfferent` | Outbound | Request payload to local server |
| `LocalOpenAIAfferent` | Inbound | Complete response from server |
| `LocalOpenAIAfferentChunk` | Inbound | Streaming response fragment |
| `LocalOpenAIStreamCompleted` | Inbound | Marks end of streaming response |
| `LocalOpenAIAck` | Internal | Position-based acknowledgement |

All entries carry `Sender`, with response entries including `Model`, `Timestamp`, and `ResponseId`.

### Gateway Connection

The gateway communicates with the local server via OpenAI-compatible HTTP:

```
┌─────────────────────────────────────────────────────────┐
│              LocalOpenAIStreamingGateway                │
│                                                         │
│  ┌─────────────┐    ┌──────────────────────────────┐   │
│  │   Config    │───▶│  HTTP Client                 │   │
│  │  - Endpoint │    │  POST /v1/chat/completions   │   │
│  │  - Model    │    │  stream: true                │   │
│  │  - Options  │    └──────────────────────────────┘   │
│  └─────────────┘                 │                      │
│                                  ▼                      │
│                     Stream chunks to journal            │
└─────────────────────────────────────────────────────────┘
```

Streams Server-Sent Events, writes `LocalOpenAIAfferentChunk` per delta, then `LocalOpenAIStreamCompleted`.

### Session Architecture

Follows the established leaf pattern with bidirectional pumps:

```
                LocalOpenAI Leaf                           Agents Branch
┌─────────────────────────────────────────┐    ┌─────────────────────────────────┐
│                                         │    │                                 │
│  LocalOpenAIScrivener                   │    │  IScrivener<AgentEntry>         │
│  ┌───────────────────────────────────┐  │    │  ┌───────────────────────────┐  │
│  │ LocalOpenAIAfferent               │──┼────┼─▶│ AgentResponse             │  │
│  │ LocalOpenAIAfferentChunk          │──┼────┼─▶│ AgentAfferentChunk        │  │
│  │                                   │  │    │  │                           │  │
│  │ LocalOpenAIEfferent               │◀─┼────┼──│ AgentPrompt               │  │
│  └───────────────────────────────────┘  │    │  └───────────────────────────┘  │
│           │                             │    │                                 │
│           ▼                             │    │                                 │
│  ┌─────────────────┐                    │    │                                 │
│  │ Gateway         │                    │    │                                 │
│  │ (HTTP to local) │                    │    │                                 │
│  └─────────────────┘                    │    │                                 │
└─────────────────────────────────────────┘    └─────────────────────────────────┘
```

The `LocalOpenAIScrivener` intercepts `LocalOpenAIEfferent` writes and dispatches to the gateway, which streams response entries back.

### Transmuter

Bidirectional `IImbuingTransmuter` mapping:

| Source | Target | Notes |
|--------|--------|-------|
| `LocalOpenAIAfferent` | `AgentResponse` | Complete response |
| `LocalOpenAIAfferentChunk` | `AgentAfferentChunk` | Streaming fragment |
| `LocalOpenAIStreamCompleted` | `AgentStreamCompleted` | Stream termination |
| `AgentPrompt` | `LocalOpenAIEfferent` | Outbound request |
| Other agent entries | `LocalOpenAIAck` | Loop prevention |

Source journal position carried as reagent for ACK generation.

### Transcript Builder

Builds conversation history from journal entries for context window:

- Filters to relevant entries (`LocalOpenAIEfferent`, `LocalOpenAIAfferent`)
- Respects `HistoryClip` configuration to limit context size
- Outputs OpenAI-format message array for API request

### Configuration

| Parameter | Required | Default | Purpose |
|-----------|----------|---------|---------|
| `Endpoint` | Yes | — | Base URL (e.g., `http://localhost:8080`) |
| `Model` | Yes | — | Model identifier (e.g., `gpt-oss:20b`) |
| `Temperature` | No | `0.7` | Sampling temperature |
| `TopP` | No | `1.0` | Nucleus sampling threshold |
| `MaxTokens` | No | — | Maximum response tokens |
| `HistoryClip` | No | — | Max transcript entries to include |
| `SystemInstruction` | No | — | System prompt |
| `TimeoutSeconds` | No | `300` | Request timeout (local inference can be slow) |

### Daemon Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│                   LocalOpenAIAgentDaemon                    │
│                                                             │
│  Start()                                                    │
│    ├── Create linked CancellationTokenSource                │
│    ├── Create LocalOpenAIAgentSession                       │
│    ├── session.StartAsync()                                 │
│    │     ├── gateway.ConnectAsync() (verify endpoint)       │
│    │     ├── Start leaf→branch pump                         │
│    │     └── Start branch→leaf pump                         │
│    └── Transition(Running)                                  │
│                                                             │
│  Shutdown()                                                 │
│    ├── Cancel session token                                 │
│    ├── session.DisposeAsync()                               │
│    │     └── Await pump completion                          │
│    └── Transition(Completed)                                │
└─────────────────────────────────────────────────────────────┘
```

### Windowing

Default policies when streaming:

| Policy | Target | Behavior |
|--------|--------|----------|
| `LocalOpenAIParagraphWindowPolicy` | Response chunks | Emit on paragraph boundary |
| `LocalOpenAIMaxLengthWindowPolicy` | Response chunks | Emit when buffer exceeds threshold |

Composite policy applies OR logic: emit when any condition is satisfied.

### DI Registration

```
AddLocalOpenAIAgents(config)
  ├── Register LocalOpenAIClientConfig
  ├── Register streaming gateway
  ├── Register session factory
  ├── Register journals (keyed inner + tapped scrivener)
  ├── Register transmuters
  ├── Register transcript builder
  ├── Register daemon
  ├── Register windowing policies
  └── Register windowing daemons

UseLocalOpenAIAgents(config)
  ├── Call AddLocalOpenAIAgents
  └── Return BranchManifest for covenant wiring
```

### Error Handling

| Condition | Behavior |
|-----------|----------|
| Connection refused | Fail daemon start; surface via `DaemonEvent` |
| Request timeout | Write fault entry; do not retry |
| Invalid response | Write fault entry with raw response for debugging |

Connection verification on `ConnectAsync()` catches endpoint misconfiguration early.

## Alternatives Considered

### Extending OpenAI Leaf with Endpoint Override

The existing `Coven.Agents.OpenAI` leaf could accept a custom `Endpoint` parameter. Rejected because:
- Cloud leaf uses official OpenAI SDK which expects `api.openai.com`
- Local timeout semantics differ (5 minutes vs 30 seconds)
- API key handling differs (often optional locally)
- Keeps concerns separated; cloud leaf remains clean

### Using HttpClient Directly in Application Code

Inline HTTP calls without a leaf. Rejected because:
- Loses journal-first design (no replay, audit, or windowing)
- Duplicates boilerplate across applications
- No standard daemon lifecycle integration

## Risks

| Risk | Mitigation |
|------|------------|
| Slow local inference causing timeouts | 5-minute default timeout; configurable |
| Memory exhaustion from large contexts | Respect `HistoryClip`; document limits |
| Server not running when daemon starts | `ConnectAsync` verifies endpoint; clear error message |

