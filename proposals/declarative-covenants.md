# Declarative Covenants

> **Status**: Proposal  
> **Created**: 2026-01-24  
> **Depends on**: [transmuter-non-null-constraint.md](transmuter-non-null-constraint.md)

---

## Problem

RouterBlock is boilerplate-heavy and error-prone:
- Manual daemon startup (forget → hang)
- Imperative journal tailing (boilerplate)
- Pattern matching on entry types (the only real logic)
- No validation (missing route → silent failure)

## Insight

RouterBlock does many things. Only one matters:

```csharp
// The routing table is the only real logic
ChatAfferent            → AgentPrompt
AgentResponse           → ChatEfferentDraft
AgentAfferentChunk      → ChatChunk
AgentThought            → (terminal)
```

Everything else—daemon startup, journal tailing, entry dispatch—is infrastructure the library should provide.

---

## Constraints

1. **Scoped to DI context** — Covenants live within a block's DI scope. Daemons registered by a covenant belong to that scope.

2. **Block→Block flow preserved** — Existing coven block composition remains. Covenants operate within a block, not across blocks.

3. **One entry = one route invocation** — Each entry triggers exactly one route. That route may produce 0, 1, or N outputs. The covenant does not perform windowing—that's the windowing layer's responsibility, upstream of the covenant.

4. **Windowing is upstream** — By the time entries reach the covenant, windowing/shattering decisions are complete. The covenant sees individual entries (including chunks if streaming is enabled) and routes them. It does not buffer, aggregate, or decide "when" to emit.

---

## Proposal

Replace RouterBlock with a declarative **Covenant** at DI time.

```csharp
builder.Services.AddCoven(coven =>
{
    // Branches return manifests declaring what they produce/consume
    var chat = coven.UseDiscordChat(discordConfig);
    var agents = coven.UseOpenAIAgents(agentConfig);
    
    // Covenant connects manifests and defines routes between them
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(
                    new AgentPrompt(msg.Sender, msg.Text)));
            
            c.Route<AgentResponse, ChatEfferentDraft>(
                (r, ct) => Task.FromResult(
                    new ChatEfferentDraft("BOT", r.Text)));
            
            c.Terminal<AgentThought>();
        });
});
```

---

## Branch Manifests

Each `UseX` extension returns a manifest declaring what the branch produces, consumes, and requires:

```csharp
public sealed record BranchManifest(
    string Name,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);  // Daemons this branch needs
```

Streaming configuration affects the manifest at registration time—if streaming is enabled, chunk types appear in `Produces`. The covenant doesn't know about streaming; it only sees the types.

```csharp
// Non-streaming: Produces = { AgentResponse, AgentThought }
// Streaming:     Produces = { AgentResponse, AgentThought, AgentAfferentChunk, AgentAfferentThoughtChunk }
var agents = coven.UseOpenAIAgents(config);
```

### Dual Streaming Pipelines

When streaming is enabled, the agent branch produces **two independent streaming pipelines**:

1. **Response stream**: `AgentAfferentChunk` → (windowed) → `AgentResponse`
2. **Thought stream**: `AgentAfferentThoughtChunk` → (windowed) → `AgentThought`

Each has independent windowing policies (paragraph boundaries vs summary markers). The manifest reflects all four types when streaming is enabled, and the covenant must route or terminate each:

```csharp
coven.Covenant()
    .Connect(agents) // streaming enabled
    .Routes(c =>
    {
        // Completed entries (post-windowing)
        c.Route<AgentResponse, ChatEfferentDraft>(...);
        c.Terminal<AgentThought>();
        
        // Raw chunks (for real-time display, if desired)
        c.Route<AgentAfferentChunk, ChatChunk>(...);
        c.Terminal<AgentAfferentThoughtChunk>();
    });
```

The covenant routes what the manifest declares. Windowing is orthogonal—it's configured on the branch, not the covenant.

---

## Covenant API

```csharp
public interface ICovenantBuilder
{
    ICovenantBuilder Connect(BranchManifest manifest);
    void Routes(Action<ICovenant> configure);
}

public interface ICovenant
{
    // Lambda routes (async all the way down)
    ICovenant Route<TSource, TTarget>(
        Func<TSource, CancellationToken, Task<TTarget>> map);
    
    // Transmuter routes (DI-resolved, inherently async)
    ICovenant Route<TSource, TTarget, TTransmuter>()
        where TTransmuter : class, ITransmuter<TSource, TTarget>;
    
    // Entry type is explicitly not routed
    ICovenant Terminal<TEntry>();
}
```

**Design notes**:
- **Async all the way** — Routes are fundamentally async. The system is long-running; sync-over-async would be worse than the small overhead of `Task.FromResult` for trivial transforms.
- **Non-nullable returns** — `Route` always produces exactly one output, matching `ITransmuter<TIn, TOut>`. No content-level filtering. This depends on the [transmuter-non-null-constraint](transmuter-non-null-constraint.md) proposal being implemented.
- **One source, one disposition** — Each source type has exactly one Route or one Terminal. No broadcasting. The covenant describes the canonical pipeline path.
- **Side-effects live elsewhere** — Logging, metrics, and other fan-out patterns use scrivener decorators or imbuing transmuters, not multiple routes. This keeps the covenant pure and the side-effects explicit.

---

## What Happens at Build Time

1. **Collect manifests** from `Connect()` calls
2. **Collect routes** from `Routes()` callback
3. **Validate completeness** against manifests:
   - Every type in any manifest's `Produces` has a Route or Terminal (no implicit ignoring)
   - Every type in any manifest's `Consumes` has a Route producing it
   - Every transmuter type referenced is registered in DI
4. **Register daemon auto-start** for all daemons in connected branches (see [daemon-scope-auto-start.md](daemon-scope-auto-start.md))
5. **Fail fast** if validation fails—no silent misconfiguration

---

## Validation Errors

```
✗ AgentResponse is produced but has no route and is not terminal.
  Add: c.Route<AgentResponse, ...>() or c.Terminal<AgentResponse>()

✗ AgentThought has both a Route and a Terminal. Choose one.
  Remove: c.Terminal<AgentThought>() or c.Route<AgentThought, ...>()

✗ AgentThought has multiple routes. Each source type may have only one route.
  The covenant describes the canonical path. For side-effects, use scrivener
  decorators or imbuing transmuters.

✗ AgentPrompt is consumed but nothing routes to it.
  Add: c.Route<..., AgentPrompt>()

✗ MyResponseTransmuter is not registered in the service container.
  Add: services.AddTransient<MyResponseTransmuter>()
```

---

## Example: DiscordAgent

```csharp
builder.Services.AddCoven(coven =>
{
    var chat = coven.UseDiscordChat(discordConfig);
    var agents = coven.UseOpenAIAgents(agentConfig); // streaming enabled via config
    
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            // Completed entries (post-windowing)
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(
                    new AgentPrompt(msg.Sender, msg.Text)));
            
            c.Route<AgentResponse, ChatEfferentDraft>(
                (r, ct) => Task.FromResult(
                    new ChatEfferentDraft("BOT", r.Text)));
            
            // Streaming chunks (required because agents manifest includes them)
            c.Route<AgentAfferentChunk, ChatChunk>(
                (chunk, ct) => Task.FromResult(
                    new ChatChunk("BOT", chunk.Text)));
            
            // Thought streams — terminal (not displayed)
            c.Terminal<AgentThought>();
            c.Terminal<AgentAfferentThoughtChunk>();
        });
});
```

**Result**: No RouterBlock. Validated at build time. Daemons auto-start within scope.

---

## Transmuter Compatibility

Existing `ITransmuter<TIn, TOut>` implementations work directly as routes—transmuters are already async with `CancellationToken`.

| Barrier | Verdict | Resolution |
|---------|---------|------------|
| **Async/CancellationToken** | ✅ Native | API is async-only; transmuters already match |
| **Non-nullable returns** | ⚠️ Requires proposal | See [transmuter-non-null-constraint.md](transmuter-non-null-constraint.md) |
| **DI dependencies** | ✅ Supported | `Route<S, T, TTransmuter>()` resolves from container |
| **Batch/Imbuing** | ✅ Out of scope | Windowing layer, not covenant |

### Why DI is allowed

If a user defines a transmuter as their business logic, they should use it directly:

```csharp
coven.Covenant(c =>
{
    // Simple: async lambda with captured config
    c.Route<ChatAfferent, AgentPrompt>(
        (msg, ct) => Task.FromResult(
            new AgentPrompt(msg.Sender, msg.Text)));
    
    // Complex: user's transmuter with DI dependencies
    c.Route<AgentResponse, ChatEfferentDraft, MyResponseTransmuter>();
});
```

The transmuter is "explicit by reference"—you know *what* transforms A→B, follow the type to see *how*.

### Explicitness tradeoff

| Approach | Explicit | Testable | DI-friendly |
|----------|----------|----------|-------------|
| Lambda only | ✅ Logic visible at registration | ✅ Easy to unit test | ❌ No scoped services |
| Transmuter type | ⚠️ Logic lives elsewhere | ✅ Test the transmuter | ✅ Full DI |

### Side-effects and purity

Routes should be **side-effect free**—they transform data, not perform IO. This is a design guideline, not an enforced constraint:

- **Lambda routes**: Easy to audit at registration
- **Transmuter routes**: Harder to audit, but if a user's transmuter does IO, that's their business logic—the covenant just executes it

The covenant validates *completeness* (all entry types routed), not *purity* (no side effects). Purity is the user's responsibility. The library's responsibility is to ensure that purity is the default.

---

## Related Proposals

- **[transmuter-non-null-constraint.md](transmuter-non-null-constraint.md)** — Required. Ensures transmuters don't use null returns for filtering.
- **[daemon-scope-auto-start.md](daemon-scope-auto-start.md)** — Describes how daemons are automatically started when entering a covenant's DI scope.
- **[daemon-magistrate.md](daemon-magistrate.md)** — Monitors daemons for transient failures after successful startup.
