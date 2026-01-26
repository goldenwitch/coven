# Gemini E2E Test Support

> **Status**: Proposal  
> **Created**: 2026-01-24

---

## Summary

Add E2E test support for the Gemini agents module, following the established patterns from OpenAI testing. This requires:

1. A `VirtualGeminiGateway` for scripting Gemini API responses
2. Builder extensions on `E2ETestHostBuilder` 
3. E2E test suite mirroring `ConsoleOpenAITests` and `ConsoleOpenAIStreamingTests`

---

## Motivation

The `Coven.Agents.Gemini` module was merged from main but lacks E2E test coverage. Without tests:

- Regressions can slip through undetected
- The declarative Covenant integration (added via `GeminiCovenBuilderExtensions`) is untested
- Streaming behavior and thought/reasoning chunk handling are unvalidated

The existing E2E test harness (`Coven.Testing.Harness`) already provides patterns for:
- Virtual gateway injection (`VirtualOpenAIGateway`, `VirtualDiscordGateway`)
- Scripted response sequences (complete, streaming, streaming with thoughts)
- Request verification (capturing sent messages)
- Scoped service provider resolution for scrivener access

Gemini support should follow these established patterns.

---

## Design

### 1. VirtualGeminiGateway

A test double implementing `IGeminiGatewayConnection` that mirrors `VirtualOpenAIGateway`:

```csharp
// Coven.Testing.Harness/VirtualGeminiGateway.cs

public sealed class VirtualGeminiGateway : IGeminiGatewayConnection
{
    private readonly Queue<IScriptedGeminiResponse> _responses = new();
    private readonly List<GeminiEfferent> _sentMessages = [];
    private readonly Lock _lock = new();
    private IServiceProvider? _scopedProvider;

    // === Scoped Provider (for scrivener resolution) ===
    
    public void SetScopedProvider(IServiceProvider? serviceProvider)
    {
        _scopedProvider = serviceProvider;
    }

    private IScrivener<GeminiEntry> GetScrivener()
    {
        IServiceProvider provider = _scopedProvider
            ?? throw new InvalidOperationException(
                "VirtualGeminiGateway cannot resolve scrivener: no active scope.");

        return provider.GetRequiredKeyedService<IScrivener<GeminiEntry>>("Coven.InternalGeminiScrivener");
    }

    // === Test Setup API ===

    public void EnqueueResponse(string content, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiCompleteResponse(content, model ?? "gemini-2.0-flash"));
        }
    }

    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiStreamingResponse([.. chunks], model ?? "gemini-2.0-flash"));
        }
    }

    public void EnqueueStreamingResponseWithReasoning(
        IEnumerable<string> reasoningChunks,
        IEnumerable<string> responseChunks,
        string? model = null)
    {
        lock (_lock)
        {
            _responses.Enqueue(new ScriptedGeminiStreamingWithReasoningResponse(
                [.. reasoningChunks],
                [.. responseChunks],
                model ?? "gemini-2.0-flash"));
        }
    }

    // === Test Output API ===

    public IReadOnlyList<GeminiEfferent> SentMessages
    {
        get
        {
            lock (_lock) { return [.. _sentMessages]; }
        }
    }

    public void ClearSentMessages()
    {
        lock (_lock) { _sentMessages.Clear(); }
    }

    public int PendingResponseCount
    {
        get
        {
            lock (_lock) { return _responses.Count; }
        }
    }

    // === IGeminiGatewayConnection Implementation ===

    public Task ConnectAsync() => Task.CompletedTask;

    public async Task SendAsync(GeminiEfferent outgoing, CancellationToken cancellationToken)
    {
        IScriptedGeminiResponse response;
        lock (_lock)
        {
            _sentMessages.Add(outgoing);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"VirtualGeminiGateway received message but no scripted response available. " +
                    $"Call EnqueueResponse() before sending. Message: '{outgoing.Text}'");
            }
            response = _responses.Dequeue();
        }

        IScrivener<GeminiEntry> scrivener = GetScrivener();
        await response.EmitAsync(scrivener, cancellationToken).ConfigureAwait(false);
    }
}
```

### 2. Scripted Response Types

Following the `IScriptedResponse` pattern from OpenAI. These types encapsulate the emission logic for each response pattern.

| Type | Input | Emits |
|------|-------|-------|
| `ScriptedGeminiCompleteResponse` | content, model | `GeminiAfferent` |
| `ScriptedGeminiStreamingResponse` | chunks[], model | `GeminiAfferentChunk`... → `GeminiStreamCompleted` → `GeminiAfferent` |
| `ScriptedGeminiStreamingWithReasoningResponse` | reasoningChunks[], responseChunks[], model | `GeminiAfferentReasoningChunk`... → `GeminiAfferentChunk`... → `GeminiStreamCompleted` → `GeminiThought` → `GeminiAfferent` |

**Implementation note**: All Gemini entry constructors require full metadata (`Sender`, `Text`, `ResponseId`, `Timestamp`, `Model`). The scripted response types generate synthetic values for `ResponseId` and `Timestamp` at emission time, matching the pattern in `VirtualOpenAIGateway`.

```csharp
// Interface mirrors IScriptedResponse from OpenAI
public interface IScriptedGeminiResponse
{
    Task EmitAsync(IScrivener<GeminiEntry> scrivener, CancellationToken cancellationToken);
}
```

The concrete implementations (`ScriptedGeminiCompleteResponse`, etc.) follow the same record-based pattern as the OpenAI scripted responses in [ScriptedResponse.cs](../src/Coven.Testing.Harness/Scripting/ScriptedResponse.cs)

### 3. E2ETestHostBuilder Extension

```csharp
// Add to E2ETestHostBuilder.cs

private bool _useVirtualGemini;

public E2ETestHostBuilder UseVirtualGemini()
{
    _useVirtualGemini = true;
    return this;
}

// In Build():
VirtualGeminiGateway? virtualGemini = null;

if (_useVirtualGemini)
{
    virtualGemini = new VirtualGeminiGateway();
    _builder.Services.RemoveAll<IGeminiGatewayConnection>();
    _builder.Services.AddSingleton<IGeminiGatewayConnection>(virtualGemini);
}

// Update E2ETestHost constructor and property:
public VirtualGeminiGateway Gemini => _gemini ?? throw new InvalidOperationException(
    "Gemini not configured. Call UseVirtualGemini() on the builder.");
```

### 4. InternalsVisibleTo

The `Coven.Agents.Gemini` module needs to expose `IGeminiGatewayConnection` to the test harness:

```xml
<!-- Coven.Agents.Gemini.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="Coven.Testing.Harness" />
</ItemGroup>
```

---

## Test Suite

### ConsoleGeminiTests.cs

Mirror the structure of `ConsoleOpenAITests`:

| Test | Description |
|------|-------------|
| `UserMessageSentToGeminiResponseAppearsOnConsole` | Basic request/response flow |
| `UserMessageCapturedByGateway` | Verify gateway captures outbound messages |
| `MultipleMessagesProcessedInOrder` | Sequential message handling |
| `ThoughtsRoutedToChat` | Reasoning/thoughts appear in chat output |

### ConsoleGeminiStreamingTests.cs

Mirror the structure of `ConsoleOpenAIStreamingTests`:

| Test | Description |
|------|-------------|
| `StreamingResponseAppearsAsChunks` | Chunks flow through correctly |
| `StreamingWithReasoningEmitsThoughtsAndResponse` | Reasoning chunks + response chunks |
| `WindowingPolicyAppliedToChunks` | Paragraph/max-length windowing |
| `MultipleStreamingExchanges` | Sequential streaming conversations |

### Example Test

```csharp
[Fact]
public async Task UserMessageSentToGeminiResponseAppearsOnConsole()
{
    // Arrange
    await using E2ETestHost host = new E2ETestHostBuilder()
        .UseVirtualConsole()
        .UseVirtualGemini()
        .ConfigureCoven(coven =>
        {
            ConsoleClientConfig consoleConfig = new()
            {
                InputSender = "console",
                OutputSender = "BOT"
            };

            GeminiClientConfig geminiConfig = new()
            {
                ApiKey = "test-key",
                Model = "gemini-2.0-flash"
            };

            BranchManifest chat = coven.UseConsoleChat(consoleConfig);
            BranchManifest agents = coven.UseGeminiAgents(geminiConfig);

            coven.Covenant()
                .Connect(chat)
                .Connect(agents)
                .Routes(c =>
                {
                    c.Route<ChatAfferent, AgentPrompt>(
                        (msg, ct) => Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));

                    c.Route<AgentResponse, ChatEfferent>(
                        (r, ct) => Task.FromResult(new ChatEfferent("BOT", r.Text)));

                    c.Route<AgentThought, ChatEfferent>(
                        (t, ct) => Task.FromResult(new ChatEfferent("BOT", t.Text)));
                });
        })
        .Build();

    host.Gemini.EnqueueResponse("Hello from Gemini!");

    await host.StartAsync();

    // Act
    await host.Console.SendInputAsync("Hello, Gemini!");

    // Assert
    string output = await host.Console.WaitForOutputContainingAsync(
        "Hello from Gemini",
        TimeSpan.FromSeconds(10));

    Assert.Contains("Hello from Gemini", output);
}
```

---

## File Structure

```
src/
├── Coven.Agents.Gemini/
│   └── Coven.Agents.Gemini.csproj    # Add InternalsVisibleTo
│
├── Coven.Testing.Harness/
│   ├── E2ETestHost.cs                # Add Gemini property
│   ├── E2ETestHostBuilder.cs         # Add UseVirtualGemini()
│   ├── VirtualGeminiGateway.cs       # NEW
│   └── Scripting/
│       ├── IScriptedGeminiResponse.cs              # NEW
│       ├── ScriptedGeminiCompleteResponse.cs       # NEW
│       ├── ScriptedGeminiStreamingResponse.cs      # NEW
│       └── ScriptedGeminiStreamingWithReasoningResponse.cs  # NEW
│
└── Coven.E2E.Tests/
    └── Toys/
        ├── ConsoleGeminiTests.cs           # NEW
        └── ConsoleGeminiStreamingTests.cs  # NEW (if streaming enabled)
```

---

## Implementation Plan

### Phase 1: Infrastructure (Testing Harness)

1. Add `InternalsVisibleTo` to `Coven.Agents.Gemini.csproj`
2. Create `IScriptedGeminiResponse` interface and implementations
3. Create `VirtualGeminiGateway`
4. Update `E2ETestHostBuilder` with `UseVirtualGemini()`
5. Update `E2ETestHost` with `Gemini` property

### Phase 2: Basic Tests

6. Create `ConsoleGeminiTests.cs` with non-streaming tests
7. Verify basic request/response flow works

### Phase 3: Streaming Tests (Optional)

8. Create `ConsoleGeminiStreamingTests.cs` if streaming support is enabled
9. Test reasoning/thought chunk handling

---

## Open Questions

1. **Gemini Entry Types**: The Gemini module uses `GeminiAfferentReasoningChunk` for thinking/reasoning. Should the scripted response types mirror OpenAI's naming (`WithThoughts`) or use Gemini's terminology (`WithReasoning`)?
   - **Recommendation**: Use `WithReasoning` to match Gemini's terminology and avoid confusion.

2. **Streaming Support**: The Gemini module has both `GeminiRequestGatewayConnection` (non-streaming) and `GeminiStreamingGatewayConnection`. Should the virtual gateway support both modes, or assume streaming is always enabled?
   - **Recommendation**: Support both via registration pattern (like OpenAI), but start with non-streaming tests.

3. **Test Parity**: Should Gemini tests exactly mirror OpenAI tests, or should we add Gemini-specific scenarios (e.g., longer reasoning chains)?
   - **Recommendation**: Start with parity, add Gemini-specific tests as needed.

---

## Alternatives Considered

### Shared Generic Virtual Gateway

Instead of `VirtualOpenAIGateway` and `VirtualGeminiGateway`, create a generic `VirtualAgentGateway<TEntry, TEfferent>`.

**Rejected because**:
- The gateway interfaces differ (`IOpenAIGatewayConnection` vs `IGeminiGatewayConnection`)
- Entry types and their semantics differ (OpenAI thoughts vs Gemini reasoning)
- Concrete types are clearer for test authors
- The duplication is minimal and explicit

### Test-Only Module

Create `Coven.Agents.Gemini.Testing` as a separate package.

**Rejected because**:
- The existing pattern places virtual gateways in `Coven.Testing.Harness`
- A separate package adds deployment/versioning complexity
- `InternalsVisibleTo` is the established pattern for accessing internal interfaces

---

## References

- [VirtualOpenAIGateway.cs](../src/Coven.Testing.Harness/VirtualOpenAIGateway.cs) — Reference implementation
- [ConsoleOpenAITests.cs](../src/Coven.E2E.Tests/Toys/ConsoleOpenAITests.cs) — Test patterns
- [GeminiAgentSession.cs](../src/Coven.Agents.Gemini/GeminiAgentSession.cs) — Gemini session architecture
- [IGeminiGatewayConnection.cs](../src/Coven.Agents.Gemini/IGeminiGatewayConnection.cs) — Interface to implement
