# Coven.Testing.Harness

Black-box E2E test infrastructure that virtualizes external dependencies (console I/O, Discord, OpenAI) to enable deterministic integration testing without real services.

## Overview

The harness enables testing complete Coven applications by replacing real external services with virtual implementations that can be scripted and inspected. Tests run in-process for speed and debuggability while exercising the same code paths as production.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Coven.Testing.Harness                       │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │VirtualConsole│  │ VirtualOpenAI│  │ VirtualDiscord       │  │
│  │              │  │              │  │                      │  │
│  │ IConsoleIO   │  │ IOpenAI...   │  │ IDiscordGateway      │  │
│  │ ChannelPipes │  │ ScriptedResp │  │ ScriptedMessages     │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                  E2ETestHostBuilder                       │  │
│  │  - Wires virtual services into DI                        │  │
│  │  - Provides test assertions                              │  │
│  │  - Manages lifecycle                                     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Components

| Component | Source | Purpose |
|-----------|--------|---------|
| `E2ETestHostBuilder` | [E2ETestHostBuilder.cs](E2ETestHostBuilder.cs) | Fluent builder for configuring test scenarios |
| `E2ETestHost` | [E2ETestHost.cs](E2ETestHost.cs) | Manages test lifecycle (start, run, dispose) |
| `VirtualConsoleIO` | [VirtualConsoleIO.cs](VirtualConsoleIO.cs) | Virtualizes stdin/stdout for console chat testing |
| `VirtualDiscordGateway` | [VirtualDiscordGateway.cs](VirtualDiscordGateway.cs) | Scripts Discord messages and captures bot responses |
| `VirtualOpenAIGateway` | [VirtualOpenAIGateway.cs](VirtualOpenAIGateway.cs) | Scripts OpenAI API responses |
| `JournalAccessor` | [JournalAccessor.cs](JournalAccessor.cs) | Inspects journal entries after test execution |
| `ChannelTextReader` | [ChannelTextReader.cs](ChannelTextReader.cs) | Adapts `Channel<string>` to `TextReader` |
| `ChannelTextWriter` | [ChannelTextWriter.cs](ChannelTextWriter.cs) | Adapts `Channel<string>` to `TextWriter` |

### Scripting & Assertions

| Component | Source | Purpose |
|-----------|--------|---------|
| `ScriptedResponse` | [Scripting/ScriptedResponse.cs](Scripting/ScriptedResponse.cs) | Configures canned OpenAI responses (complete or streaming) |
| `E2ETestHostAssertions` | [Assertions/E2ETestHostAssertions.cs](Assertions/E2ETestHostAssertions.cs) | Fluent assertion helpers for test verification |

## Usage

### Prerequisites

Reference `Coven.Testing.Harness` from your test project. The harness depends on `xunit` for assertion infrastructure.

### Test Pattern

1. **Build** a test host with virtual gateways using `E2ETestHostBuilder`
2. **Script** expected responses before starting
3. **Start** the host (awaits daemon readiness)
4. **Send** input via virtual consoles or gateways
5. **Assert** on outputs and journal state
6. **Dispose** the host (handles graceful shutdown)

For working examples, see the E2E test suites:
- [ConsoleChatTests.cs](../Coven.E2E.Tests/Toys/ConsoleChatTests.cs) - Basic echo chat
- [ConsoleOpenAITests.cs](../Coven.E2E.Tests/Toys/ConsoleOpenAITests.cs) - OpenAI agent integration
- [ConsoleOpenAIStreamingTests.cs](../Coven.E2E.Tests/Toys/ConsoleOpenAIStreamingTests.cs) - Streaming responses
- [DiscordChatTests.cs](../Coven.E2E.Tests/Toys/DiscordChatTests.cs) - Discord chat testing
- [FileScrivenerConsoleTests.cs](../Coven.E2E.Tests/Toys/FileScrivenerConsoleTests.cs) - File persistence testing

## Virtual Gateways

### VirtualConsoleIO

Virtualizes stdin/stdout by bridging `Channel<string>` to `TextReader`/`TextWriter`. When the console daemon calls `ReadLineAsync()`, it reads from the test's input channel. When it writes output, the test captures it from the output channel.

Key methods for test control:
- `SendInputAsync(line)` - Queues input for the stdin pump
- `CompleteInput()` - Signals EOF (breaks the stdin read loop)
- `WaitForOutputAsync(timeout)` - Awaits the next output line
- `CollectOutputAsync(count, timeout)` - Collects multiple output lines
- `DrainOutput()` - Returns all buffered output without waiting

### VirtualOpenAIGateway

Scripts OpenAI API responses that flow through the standard scrivener mechanism. The gateway injects responses into the internal OpenAI scrivener using the same entry types (`OpenAIAfferentChunk`, `OpenAIStreamCompleted`) as the real gateway.

Key methods:
- `EnqueueResponse(content)` - Scripts a complete response
- `EnqueueStreamingResponse(chunks)` - Scripts a streaming response with chunk boundaries
- `EnqueueStreamingResponseWithThoughts(thoughts, response)` - Scripts reasoning + response
- `SentMessages` - Captures all outbound prompts for verification

### VirtualDiscordGateway

Scripts Discord messages and captures bot responses. Uses the `IDiscordGateway` abstraction to intercept inbound/outbound message flow.

Key methods:
- `SimulateMessageAsync(channelId, author, content)` - Simulates an incoming Discord message
- `CompleteInbound()` - Signals end of scripted messages
- `SentMessages` - Captures all messages the bot attempted to send

## Lifecycle Management

### Startup

`E2ETestHost.StartAsync()` performs:
1. Starts the underlying `IHost`
2. Waits for all registered daemons to reach `Running` status
3. Allows virtual gateways to stabilize

Configurable via `E2ETestHostBuilder.WithStartupTimeout(TimeSpan)`.

### Shutdown

`E2ETestHost.DisposeAsync()` performs:
1. Signals input completion to unblock stdin pumps
2. Requests graceful stop via `IHost.StopAsync()`
3. Aggregates any pump exceptions for test diagnostics

Configurable via `E2ETestHostBuilder.WithShutdownTimeout(TimeSpan)`.

### Timeout Protection

All public wait methods enforce timeouts to prevent test hangs. Default timeout is 30 seconds for startup, 10 seconds for shutdown, and 5 seconds for individual assertions.

## Journal Inspection

`JournalAccessor` provides typed access to all registered journals for post-execution verification:

- `Chat` - Chat entry journal (`IScrivener<ChatEntry>`)
- `Console` - Console entry journal
- `OpenAI` - OpenAI entry journal
- `Discord` - Discord entry journal
- `Get<TEntry>()` - Generic accessor for any entry type

## Streaming Tests

When testing streaming flows, be aware of the layered buffering:

```
VirtualOpenAIGateway    →    StreamWindowingDaemon    →    Observable Output
   (raw chunks)              (policy aggregation)         (user-visible)
```

The virtual gateway scripts RAW chunks at the API boundary. Window policies aggregate these into user-visible outputs. Test strategies:

1. **Fixed test policies** - Override windowing with immediate emission for deterministic tests
2. **Policy-aware assertions** - Assert on combined content without assuming boundaries
3. **Aligned chunk boundaries** - Script chunks that match policy boundaries

## File System Isolation

The harness replaces `FileScrivener` with `InMemoryScrivener` by default. This provides:
- No cleanup required between tests
- Parallelism-safe by design (isolated DI containers)
- Faster execution

Use `E2ETestHostBuilder.UseInMemoryScrivener<TEntry>()` to explicitly configure this for specific entry types.

## Covenant Configuration

Tests configure covenants through `E2ETestHostBuilder.ConfigureCoven(Action<CovenServiceBuilder>)`. The harness does **not** bypass covenant validation—tests exercise the same routing constraints as production code.

For maintainability, consider extracting shared covenant configuration into reusable methods that both toys/samples and tests can reference.
