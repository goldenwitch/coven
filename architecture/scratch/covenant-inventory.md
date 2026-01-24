# Covenant Inventory & Design Notes

> **Status**: Working document  
> **Created**: 2026-01-23

---

## Current Covenants

### ChatCovenant ✅ (Implemented)

**Location**: `Coven.Chat`

| Entry Type | Marker | Role |
|------------|--------|------|
| `ChatAfferent` | `ICovenantEntry + ICovenantSource` | Input from users |
| `ChatChunk` | `ICovenantEntry + ICovenantSource` | Streaming chunks |
| `ChatEfferent` | `ICovenantEntry + ICovenantSink` | Output to users |
| `ChatEfferentDraft` | None (draft) | Internal |
| `ChatAfferentDraft` | None (draft) | Internal |
| `ChatAck` | None | Internal sync |
| `ChatStreamCompleted` | None | Internal signal |

---

### AgentCovenant ❌ (Needed)

**Location**: `Coven.Agents`

| Entry Type | Current | Proposed |
|------------|---------|----------|
| `AgentPrompt` | None | `ICovenantSource` |
| `AgentResponse` | None | `ICovenantSink` |
| `AgentThought` | None | `ICovenantSink` |
| `AgentAfferentChunk` | None | `ICovenantSource` |
| `AgentAfferentThoughtChunk` | None | `ICovenantSource` |
| `AgentEfferentChunk` | None | Internal |
| `AgentAck` | None | Internal |
| `AgentStreamCompleted` | None | Internal |

---

## The RouterBlock Problem

### Current Pattern

RouterBlock appears in multiple toys/samples and does this:

```csharp
// RouterBlock bridges two separate journals
IScrivener<ChatEntry> _chat;
IScrivener<AgentEntry> _agents;

// Direction 1: Chat → Agent
if (entry is ChatAfferent userInput)
    await _agents.WriteAsync(new AgentPrompt(userInput.Text));

// Direction 2: Agent → Chat  
if (entry is AgentResponse response)
    await _chat.WriteAsync(new ChatEfferent(response.Text));
```

### The Question

**Can RouterBlock itself describe a covenant?**

This is interesting because:
1. RouterBlock is a **bridge** between two protocols
2. It has clear inputs (ChatAfferent) and outputs (ChatEfferent, AgentPrompt)
3. It defines a **contract** about how the protocols connect

---

## Design Exploration: Bridge Covenant

### Option A: RouterBlock implements multiple covenants

```csharp
// RouterBlock participates in BOTH covenants
public class RouterBlock : IMagikBlock, 
    ICovenantBridge<ChatCovenant, AgentCovenant>
{
    // Consumes from ChatCovenant
    // Produces to AgentCovenant
    // Consumes from AgentCovenant  
    // Produces to ChatCovenant
}
```

**Problem**: This doesn't capture the bidirectional nature cleanly.

---

### Option B: Composite Covenant

```csharp
// A covenant that composes two others
public sealed class ChatAgentBridgeCovenant : ICovenant
{
    public static string Name => "ChatAgentBridge";
}

// The bridge entries belong to THIS covenant
public record BridgedPrompt(string Text) 
    : ICovenantEntry<ChatAgentBridgeCovenant>,
      ICovenantSource<ChatAgentBridgeCovenant>;  // Comes from Chat

public record BridgedResponse(string Text)
    : ICovenantEntry<ChatAgentBridgeCovenant>,
      ICovenantSink<ChatAgentBridgeCovenant>;    // Goes to Chat
```

**Problem**: Duplicates entry types. Doesn't reuse existing Chat/Agent entries.

---

### Option C: Multi-Covenant Membership

Allow entries to belong to multiple covenants:

```csharp
// ChatAfferent is a source in ChatCovenant
// AND an input to the bridge
public record ChatAfferent(string Sender, string Text) 
    : ChatEntry(Sender), 
      ICovenantEntry<ChatCovenant>, ICovenantSource<ChatCovenant>,
      ICovenantEntry<ChatAgentBridgeCovenant>;  // Also participates here
```

**Problem**: Gets noisy. Every entry needs markers for every covenant it touches.

---

### Option D: Covenant Composition at Registration

Keep entries simple, define bridges at DI registration:

```csharp
services.AddCovenant<ChatCovenant>(...);
services.AddCovenant<AgentCovenant>(...);

// NEW: Explicit bridge registration
services.AddCovenantBridge<ChatCovenant, AgentCovenant>(bridge =>
{
    // ChatCovenant.Source → AgentCovenant.Source
    bridge.Forward<ChatAfferent, AgentPrompt>(
        transform: chat => new AgentPrompt(chat.Text));
    
    // AgentCovenant.Sink → ChatCovenant.Sink
    bridge.Reverse<AgentResponse, ChatEfferent>(
        transform: agent => new ChatEfferent(agent.Text));
});
```

**This feels right because**:
1. Entries stay simple (single covenant membership)
2. Bridge is explicit and validated
3. The transform is declarative
4. Analyzer can verify both covenants are wired correctly

---

### Option E: RouterBlock AS a Covenant

What if the block itself is the covenant definition?

```csharp
[Covenant("ChatAgentRouter")]
public class RouterBlock : IMagikBlock
{
    [CovenantSource] 
    public void HandleChatInput(ChatAfferent input) { ... }
    
    [CovenantSink]
    public Task<ChatEfferent> ProduceOutput() { ... }
    
    [CovenantSource]
    public Task<AgentPrompt> ForwardToAgent() { ... }
    
    [CovenantSink]
    public void HandleAgentResponse(AgentResponse response) { ... }
}
```

**Problem**: Attributes on methods is a different paradigm. Might conflict with MagikBlock patterns.

---

## Leaning Toward: Option D

**Covenant Composition at Registration** seems to:
- Preserve the simplicity of marker interfaces
- Make bridges explicit and verifiable
- Keep entry types focused on their primary covenant
- Enable the analyzer to check both ends

### Sketch of Bridge Builder

```csharp
public interface ICovenantBridgeBuilder<TSource, TSink>
    where TSource : ICovenant
    where TSink : ICovenant
{
    /// <summary>
    /// Forward an entry from source covenant to sink covenant.
    /// </summary>
    void Forward<TIn, TOut>(Func<TIn, TOut> transform)
        where TIn : ICovenantEntry<TSource>
        where TOut : ICovenantEntry<TSink>;
    
    /// <summary>
    /// Reverse an entry from sink covenant back to source covenant.
    /// </summary>
    void Reverse<TIn, TOut>(Func<TIn, TOut> transform)
        where TIn : ICovenantEntry<TSink>
        where TOut : ICovenantEntry<TSource>;
}
```

### What This Enables

```csharp
// Registration validates the complete flow:
services.AddCovenant<ChatCovenant>(chat => { ... });
services.AddCovenant<AgentCovenant>(agent => { ... });
services.AddCovenantBridge<ChatCovenant, AgentCovenant>(bridge =>
{
    bridge.Forward<ChatAfferent, AgentPrompt>(...);
    bridge.Reverse<AgentResponse, ChatEfferent>(...);
    bridge.Reverse<AgentThought, ChatEfferent>(...);  // Thoughts also become chat
});

// Analyzer verifies:
// - ChatAfferent (ChatCovenant source) has a forward path
// - AgentPrompt (AgentCovenant source) is produced by the bridge
// - AgentResponse (AgentCovenant sink) has a reverse path
// - ChatEfferent (ChatCovenant sink) is produced by the bridge
// - No orphans, no dead letters ACROSS the bridge
```

---

## Open Questions

1. **Does the bridge need its own daemon?** Or does it compose existing daemons?

2. **Multi-hop bridges?** Chat → Agent → Tool → Agent → Chat?

3. **Async transforms?** The bridge transform might need to be async.

4. **Error handling?** What happens when a bridge transform fails?

5. **Should bridges be bidirectional by default?** Or explicit Forward/Reverse?

---

## The N-ary Problem

### Insight: Transmuters are 2-ary, but MagikBlocks are N-ary

The current model:
- `IBatchTransmuter<TIn, TOut>` — one input type, one output type
- `ITransmuter<TIn, TOut>` — same, 1:1

But real MagikBlocks often have **multiple output branches**:

```
                    ┌──→ ChatEfferent (to user)
                    │
AgentResponse ──────┼──→ ToolInvocation (to tool system)
                    │
                    └──→ AgentPrompt (chain back to AI)
```

This is a **1:3 junction**, not a 2-ary transform.

### The Tools Case (Primary Use Case)

The library's main purpose right now is:

```
User ──→ Chat ──→ Agent ──→ AI
              ↑         │
              │         ├──→ Response (back to user)
              │         │
              │         └──→ Tool Call ──→ Tool Execution
              │                                │
              └────────────────────────────────┘
                        (tool result)
```

This is a **3-participant graph** with **4+ directed edges**:
1. `ChatAfferent` → `AgentPrompt` (user asks)
2. `AgentResponse` → `ChatEfferent` (AI responds)
3. `AgentResponse` → `ToolInvocation` (AI invokes action)
4. `ToolResult` → `AgentPrompt` (result feeds back)

No 2-ary composition can capture this. We need **N-ary junctions**.

---

## Design Exploration: N-ary Junctions

### Option D-Extended: Junction Builder

Extend the covenant builder with explicit junction points:

```csharp
services.AddCovenant<AppCovenant>(covenant =>
{
    // Boundaries
    covenant.Source<ChatAfferent>();
    covenant.Sink<ChatEfferent>();
    covenant.Sink<ToolInvocation>();
    
    // 2-ary edges (existing)
    covenant.Transform<ChatAfferent, AgentPrompt>(...);
    covenant.Transform<ToolResult, AgentPrompt>(...);
    
    // N-ary junction (NEW)
    covenant.Junction<AgentResponse>(junction =>
    {
        junction.Route<ChatEfferent>(
            when: r => !r.HasToolCalls,
            transform: r => new ChatEfferent(r.Text));
        
        junction.Route<ToolInvocation>(
            when: r => r.HasToolCalls,
            transform: r => new ToolInvocation(r.ToolCalls));
        
        junction.Route<AgentPrompt>(
            when: r => r.RequiresFollowUp,
            transform: r => new AgentPrompt(r.FollowUpContext));
    });
});
```

**Key insight**: A junction is an entry type with **conditional, multi-output routing**.

### Option E: Graph-First Covenant

What if the covenant IS the complete application graph?

```csharp
services.AddCovenant<AppCovenant>(graph =>
{
    // Declare all participants
    graph.Source<ChatAfferent>("user-input");
    graph.Sink<ChatEfferent>("user-output");
    graph.Sink<ToolInvocation>("tool-calls");
    graph.Node<AgentPrompt>();
    graph.Node<AgentResponse>();
    graph.Node<ToolResult>();
    
    // Declare all edges
    graph.Edge("user-input", "AgentPrompt", transform: ...);
    graph.Edge("AgentResponse", "user-output", when: ...);
    graph.Edge("AgentResponse", "tool-calls", when: ...);
    graph.Edge("ToolResult", "AgentPrompt");
    
    // Validator ensures:
    // - All nodes reachable from sources
    // - All nodes reach sinks
    // - No orphans, no dead letters
});
```

This is essentially a **state machine definition**.

### Option F: Entry-as-Junction

What if certain entry types declare themselves as junctions?

```csharp
// AgentResponse is inherently a junction point
public record AgentResponse(string Text, ToolCall[]? ToolCalls)
    : AgentEntry,
      ICovenantJunction<AgentCovenant>  // NEW marker
{
    // The type itself advertises: "I have multiple downstream paths"
}
```

Then the builder knows to expect routing logic for this type.

---

## The Subgraph Insight

> "Every journal edge is a connection to a subgraph"

This reframes everything:

```
┌─────────────────────────────────────────────────────────────┐
│                     Application Covenant                      │
│                                                               │
│  ┌──────────┐     ┌──────────┐     ┌──────────┐            │
│  │   Chat   │────▶│  Agent   │────▶│  Tools   │            │
│  │ Subgraph │◀────│ Subgraph │◀────│ Subgraph │            │
│  └──────────┘     └──────────┘     └──────────┘            │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

Each subgraph (Chat, Agent, Tools) is its own covenant with internal structure.
The **edges between subgraphs** are the junction points.

This suggests a hierarchical model:
1. **Leaf covenants**: ChatCovenant, AgentCovenant, ToolCovenant (internal structure)
2. **Composite covenant**: AppCovenant (wires the leaves together via junctions)

```csharp
// Leaf covenants define internal flows
services.AddCovenant<ChatCovenant>(...);
services.AddCovenant<AgentCovenant>(...);
services.AddCovenant<ToolCovenant>(...);

// Composite covenant wires them together
services.AddCompositeCovenant<AppCovenant>(composite =>
{
    composite.Include<ChatCovenant>();
    composite.Include<AgentCovenant>();
    composite.Include<ToolCovenant>();
    
    // Junction: Chat.Sink → Agent.Source
    composite.Connect<ChatAfferent, AgentPrompt>(...);
    
    // Junction: Agent.Sink → Chat.Source OR Tool.Source (N-ary!)
    composite.Junction<AgentResponse>(j =>
    {
        j.To<ChatEfferent>(...);
        j.To<ToolInvocation>(...);
    });
    
    // Junction: Tool.Sink → Agent.Source
    composite.Connect<ToolResult, AgentPrompt>(...);
});
```

---

## Can We Capture This Complexity?

**Yes, if we accept that:**

1. **Covenants are composable** — leaf + composite
2. **Junctions are first-class** — not just 2-ary transforms
3. **Routing is declarative** — `when:` predicates on junction outputs
4. **The graph is the covenant** — not just per-protocol, but per-application

**The key abstraction gap** is:
- Current: `Transform<TIn, TOut>` — 1:1
- Needed: `Junction<TIn, TOut1, TOut2, ...>` — 1:N with predicates

### Minimal Extension

To capture the tools case, we need at minimum:

```csharp
public interface ICovenantBuilder<TCovenant>
{
    // Existing 2-ary
    ICovenantBuilder<TCovenant> Transform<TIn, TOut>(...);
    ICovenantBuilder<TCovenant> Window<TIn, TOut>(...);
    
    // NEW: N-ary junction
    ICovenantBuilder<TCovenant> Junction<TIn>(
        Action<IJunctionBuilder<TCovenant, TIn>> configure);
}

public interface IJunctionBuilder<TCovenant, TIn>
{
    IJunctionBuilder<TCovenant, TIn> Route<TOut>(
        Func<TIn, bool> when,
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>;
}
```

This lets us express:
```csharp
covenant.Junction<AgentResponse>(j =>
{
    j.Route<ChatEfferent>(
        when: r => !r.HasToolCalls, 
        transform: r => new ChatEfferent(r.Text));
    
    j.Route<ToolInvocation>(
        when: r => r.HasToolCalls,
        transform: r => r.ToolCalls.Select(t => new ToolInvocation(t)));
});
```

---

## Revised Mental Model

```
Covenant = Graph of Entry Types

Nodes:
  - Source (boundary in)
  - Sink (boundary out)  
  - Internal

Edges:
  - Transform (1:1)
  - Window (N:1)
  - Junction (1:N with predicates)
  - Shatter (1:N unconditional)

Composition:
  - Leaf covenant (single protocol)
  - Composite covenant (wires leaves via junctions)
```

The **transmuter is the 2-ary case**. The **junction is the N-ary case**.

---

## Next Steps

1. [x] Validate Junction design against toys/samples
2. [ ] Implement AgentCovenant with markers
3. [ ] Implement IJunctionBuilder interface  
4. [ ] Add Junction support to CovenantBuilder
5. [ ] Update toys/samples to use covenant registrations

---

## Validation Results (2026-01-23)

**Verdict: YES with caveats** — Junction design captures all existing toys/samples.

### What Works Perfectly

| Project | Pattern | Fit |
|---------|---------|-----|
| ConsoleChat | Transform only | ✅ |
| DiscordChat | Transform only | ✅ |
| DiscordStreaming | Transform + Window | ✅ |
| FileScrivenerConsole | Transform only | ✅ |

### What Needs Junction

| Project | Junction Needed | Pattern |
|---------|-----------------|---------|
| ConsoleOpenAI | `Junction<AgentEntry>` | AgentResponse/AgentThought → ChatEfferent |
| ConsoleOpenAIStreaming | `Junction<AgentEntry>` | Same + windowing |
| 01.DiscordAgent | `Junction<AgentEntry>` | Same, outputs Draft |

### Identified Gaps (Addressed Below)

1. **Cross-journal bridging** — Chat ↔ Agent spans two scriveners
2. **Junction fallthrough** — What happens to unmatched entries?
3. **Type constraints** — AgentEntry subtypes need covenant markers

---

## Finalized Design

### Core Insight

A **Covenant** is a directed graph where:
- **Nodes** = Entry types
- **Edges** = Transforms, Windows, Junctions, Shatters

```
┌─────────────────────────────────────────────────────────────┐
│                      AppCovenant                             │
│                                                              │
│  [Source]         [Transform]        [Junction]    [Sink]   │
│  ChatAfferent ────────────────▶ AgentPrompt                 │
│                                      │                       │
│                                      ▼                       │
│  [Source]         [Window]      AgentResponse               │
│  AgentChunk  ─────────────────▶     │                       │
│                                      ├──▶ ChatEfferent ────▶│
│                                      │    (when: !tools)     │
│                                      │                       │
│                                      └──▶ ToolInvocation ──▶│
│                                           (when: tools)      │
│                                                              │
│  [Source]         [Transform]                               │
│  ToolResult  ─────────────────▶ AgentPrompt (cycle back)    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Builder Interface

```csharp
public interface IStreamingCovenantBuilder<TCovenant> : ICovenantBuilder<TCovenant>
    where TCovenant : ICovenant
{
    // Existing
    IStreamingCovenantBuilder<TCovenant> Source<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSource<TCovenant>;
    
    IStreamingCovenantBuilder<TCovenant> Sink<TEntry>()
        where TEntry : ICovenantEntry<TCovenant>, ICovenantSink<TCovenant>;
    
    IStreamingCovenantBuilder<TCovenant> Transform<TIn, TOut>(
        ITransmuter<TIn, TOut> transmuter)
        where TIn : ICovenantEntry<TCovenant>
        where TOut : ICovenantEntry<TCovenant>;
    
    IStreamingCovenantBuilder<TCovenant> Window<TChunk, TOut>(
        IWindowPolicy<TChunk> policy,
        IBatchTransmuter<TChunk, TOut> transmuter,
        IShatterPolicy<TOut>? shatter = null)
        where TChunk : ICovenantEntry<TCovenant>
        where TOut : ICovenantEntry<TCovenant>;
    
    // NEW: N-ary junction
    IStreamingCovenantBuilder<TCovenant> Junction<TIn>(
        Action<IJunctionBuilder<TCovenant, TIn>> configure)
        where TIn : ICovenantEntry<TCovenant>;
}

public interface IJunctionBuilder<TCovenant, TIn>
    where TCovenant : ICovenant
    where TIn : ICovenantEntry<TCovenant>
{
    /// <summary>
    /// Route entries matching the predicate to the output type.
    /// </summary>
    IJunctionBuilder<TCovenant, TIn> Route<TOut>(
        Func<TIn, bool> when,
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>;
    
    /// <summary>
    /// Route entries matching the predicate to multiple outputs.
    /// </summary>
    IJunctionBuilder<TCovenant, TIn> RouteMany<TOut>(
        Func<TIn, bool> when,
        Func<TIn, IEnumerable<TOut>> transform)
        where TOut : ICovenantEntry<TCovenant>;
    
    /// <summary>
    /// Default route for entries not matching any predicate.
    /// If not specified, unmatched entries are dropped.
    /// </summary>
    IJunctionBuilder<TCovenant, TIn> Default<TOut>(
        Func<TIn, TOut> transform)
        where TOut : ICovenantEntry<TCovenant>;
}
```

### Cross-Journal Bridging

For Chat ↔ Agent, we have two options:

**Option A: Single Spanning Covenant (Recommended)**

All entry types belong to one `AppCovenant`:

```csharp
// All entries implement ICovenantEntry<AppCovenant>
public record ChatAfferent(...) : ChatEntry, ICovenantEntry<AppCovenant>, ICovenantSource<AppCovenant>;
public record AgentPrompt(...) : AgentEntry, ICovenantEntry<AppCovenant>;
public record AgentResponse(...) : AgentEntry, ICovenantEntry<AppCovenant>;
public record ChatEfferent(...) : ChatEntry, ICovenantEntry<AppCovenant>, ICovenantSink<AppCovenant>;
```

**Option B: Multi-Covenant with Bridge**

Keep separate covenants, add explicit bridge:

```csharp
services.AddCovenant<ChatCovenant>(...);
services.AddCovenant<AgentCovenant>(...);
services.AddCovenantBridge<ChatCovenant, AgentCovenant>(bridge =>
{
    bridge.Forward<ChatAfferent, AgentPrompt>(...);
    bridge.Reverse<AgentResponse, ChatEfferent>(...);
});
```

**Decision**: Start with Option A (simpler). Option B can be added later for modularity.

### Junction Validation Rules

The validator checks:

1. **Coverage**: Every route's output type must be either:
   - Consumed by another Transform/Window/Junction, OR
   - A declared Sink

2. **Reachability**: Every junction input must be:
   - Produced by a Transform/Window, OR
   - A declared Source

3. **No Dead Routes**: At least one route must be defined

4. **Predicate Exhaustiveness**: Warning if predicates don't cover all cases (optional)

---

## Example: Full DiscordAgent Covenant

```csharp
public sealed class DiscordAgentCovenant : ICovenant
{
    public static string Name => "DiscordAgent";
}

services.AddCovenant<DiscordAgentCovenant>(covenant =>
{
    // Boundaries
    covenant.Source<ChatAfferent>();
    covenant.Source<AgentAfferentChunk>();
    covenant.Source<ToolResult>();
    covenant.Sink<ChatEfferent>();
    covenant.Sink<ToolInvocation>();
    
    // Chat input → Agent prompt
    covenant.Transform<ChatAfferent, AgentPrompt>(
        new ChatToAgentPromptTransmuter());
    
    // Tool result → Agent prompt (feedback loop)
    covenant.Transform<ToolResult, AgentPrompt>(
        new ToolResultToPromptTransmuter());
    
    // Streaming chunks → Windowed response
    covenant.Window<AgentAfferentChunk, AgentResponse>(
        policy: new AgentParagraphWindowPolicy(),
        transmuter: new AgentAfferentBatchTransmuter());
    
    // Response routing (the N-ary junction)
    covenant.Junction<AgentResponse>(j =>
    {
        // Text responses → Chat output
        j.Route<ChatEfferentDraft>(
            when: r => !r.HasToolCalls,
            transform: r => new ChatEfferentDraft("assistant", r.Text));
        
        // Tool calls → Tool system
        j.RouteMany<ToolInvocation>(
            when: r => r.HasToolCalls,
            transform: r => r.ToolCalls.Select(t => new ToolInvocation(t)));
    });
    
    // Draft → Shatter → Chunk → Window → Effluent (existing streaming)
    covenant.Window<ChatChunk, ChatEfferent>(
        policy: new ChatParagraphWindowPolicy(),
        transmuter: new ChatChunkBatchTransmuter());
});
```

---

## Projects Using RouterBlock

| Project | Bridge Pattern | Covenant Model |
|---------|---------------|----------------|
| Toys.ConsoleOpenAI | Chat ↔ Agent | Single AppCovenant with Junction |
| Toys.ConsoleOpenAIStreaming | Chat ↔ Agent (streaming) | Single AppCovenant with Window + Junction |
| Sample.DiscordAgent | Chat ↔ Agent ↔ Tools | Single AppCovenant with Junction + RouteMany |
