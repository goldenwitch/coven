# Daemon Scope Auto-Start

> **Status**: Proposal  
> **Created**: 2026-01-24  
> **Depends on**: [declarative-covenants.md](declarative-covenants.md)

---

## Problem

Manual daemon startup is error-prone boilerplate that plagues every block in the codebase:

```csharp
// This pattern appears in EVERY block that uses daemons
public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
{
    // Boilerplate: start all daemons
    foreach (ContractDaemon d in _daemons)
    {
        await d.Start(ct).ConfigureAwait(false);
    }
    
    // ... actual business logic ...
}
```

The consequences of forgetting this boilerplate are severe:

| Failure Mode | Symptom | Diagnosis Difficulty |
|--------------|---------|---------------------|
| Forget to start daemons | Silent hang — journal never receives entries | Hard — no errors, just nothing happening |
| Start daemons in wrong order | Race conditions, missed entries | Very hard — intermittent failures |
| Start some but not all | Partial functionality, confusing behavior | Hard — some things work, others don't |

This is a **pit of failure**. The default path (forgetting the boilerplate) leads to broken behavior. The correct path requires remembering incantations that have nothing to do with the block's actual purpose.

### Evidence from Codebase

Every RouterBlock, EchoBlock, and similar construct contains identical daemon startup code:

- [RouterBlock.cs](../src/toys/Coven.Toys.ConsoleOpenAIStreaming/RouterBlock.cs#L21-L25)
- [EchoBlock.cs](../src/toys/Coven.Toys.DiscordChat/EchoBlock.cs#L16-L19)
- [StreamingBlock.cs](../src/toys/Coven.Toys.DiscordStreaming/StreamingBlock.cs#L17-L21)

Each repeats the same pattern. Each is a place where someone could forget. Each is a place where silent failure awaits.

---

## Goals

1. **Automatic startup** — Daemons start automatically when their DI scope activates
2. **No daemon changes** — Existing `IDaemon` and `ContractDaemon` implementations work unchanged
3. **DI-native** — Works with standard `IServiceScope` patterns
4. **Fail fast** — If any daemon fails to start, the scope activation fails immediately
5. **Deterministic ordering** — Daemons start in a predictable, configurable order
6. **Clean shutdown** — When scope ends, daemons are stopped gracefully

---

## Non-Goals

- **Orchestrating daemon dependencies** — If daemon B depends on daemon A being in a specific state, that's the [daemon-magistrate.md](daemon-magistrate.md)'s concern, not scope entry
- **Retry on startup failure** — Startup failures are immediate failures; transient recovery is the magistrate's domain
- **Hot-swapping daemons** — Daemons are scoped to the DI scope; replacing them means creating a new scope

---

## Design Options

### Option 1: IServiceScope Decoration

Wrap `IServiceScope` to intercept scope creation and start daemons:

```csharp
internal sealed class DaemonAwareServiceScope : IServiceScope
{
    private readonly IServiceScope _inner;
    private readonly IReadOnlyList<IDaemon> _daemons;
    
    public IServiceProvider ServiceProvider => _inner.ServiceProvider;
    
    internal static async Task<DaemonAwareServiceScope> CreateAsync(
        IServiceScope inner,
        CancellationToken ct)
    {
        var daemons = inner.ServiceProvider
            .GetServices<ContractDaemon>()
            .ToList();
        
        // Start all daemons before returning the scope
        await StartDaemonsAsync(daemons, ct);
        
        return new DaemonAwareServiceScope(inner, daemons);
    }
    
    public void Dispose()
    {
        // Shutdown daemons before disposing scope
        ShutdownDaemons(_daemons);
        _inner.Dispose();
    }
}
```

**Pros**: Clean separation, testable
**Cons**: Requires async scope creation (not supported by `IServiceScopeFactory`)

### Option 2: AsyncLocal Scope with Async Entry

Modify `CovenExecutionScope` to be async-aware:

```csharp
internal static class CovenExecutionScope
{
    private static readonly AsyncLocal<DaemonScope?> _currentScope = new();
    
    internal static IServiceProvider? CurrentProvider 
        => _currentScope.Value?.Scope.ServiceProvider;
    
    internal static async Task<DaemonScope> BeginScopeAsync(
        IServiceProvider root, 
        CancellationToken ct)
    {
        var scopeFactory = root.GetRequiredService<IServiceScopeFactory>();
        var scope = scopeFactory.CreateScope();
        
        var daemons = scope.ServiceProvider
            .GetServices<ContractDaemon>()
            .ToList();
        
        // Start daemons as part of scope entry
        await StartDaemonsInOrderAsync(daemons, ct);
        
        var daemonScope = new DaemonScope(scope, daemons);
        _currentScope.Value = daemonScope;
        return daemonScope;
    }
    
    internal static async Task EndScopeAsync(DaemonScope? scope, CancellationToken ct)
    {
        if (scope is null) return;
        
        try
        {
            await ShutdownDaemonsAsync(scope.Daemons, ct);
        }
        finally
        {
            scope.Scope.Dispose();
            _currentScope.Value = null;
        }
    }
}

internal sealed record DaemonScope(
    IServiceScope Scope, 
    IReadOnlyList<ContractDaemon> Daemons) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await CovenExecutionScope.EndScopeAsync(this, CancellationToken.None);
    }
}
```

**Pros**: Natural fit with existing `CovenExecutionScope` pattern  
**Cons**: Changes `BeginScope` signature to async

### Option 3: Covenant-Driven Daemon Discovery

Let the covenant declare which daemons are needed based on connected manifests:

```csharp
public sealed record BranchManifest(
    string Name,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);  // NEW: daemons this branch needs
```

The covenant builder collects daemon requirements:

```csharp
public interface ICovenantBuilder
{
    ICovenantBuilder Connect(BranchManifest manifest);
    void Routes(Action<ICovenant> configure);
    
    // Internally tracks: union of all RequiredDaemons from connected manifests
}
```

**Pros**: Explicit about what daemons are needed, validation at build time  
**Cons**: Requires manifest changes, more moving parts

### Recommendation: Option 2 + Option 3

Use **Option 2** (async scope entry) as the mechanism, informed by **Option 3** (covenant-driven discovery) for knowing *which* daemons to start.

The covenant knows the full picture at build time. It can validate that required daemons are registered and configure the scope entry to start exactly those daemons.

---

## Daemon Startup Ordering

### The Problem

What if daemons have startup dependencies?

```
DiscordDaemon ──────► needs connection before...
OpenAIDaemon ──────► can start independently
SegmentationDaemon ► needs OpenAIDaemon's model loaded first
```

### Options

**A. Declaration Order** — Start daemons in the order branches are connected:

```csharp
coven.Covenant()
    .Connect(discord)   // DiscordDaemon starts first
    .Connect(agents)    // OpenAIDaemon starts second
    .Routes(c => ...);
```

**Pros**: Simple, explicit  
**Cons**: Conflates logical connection order with startup order

**B. Explicit Startup Order** — Separate API for ordering:

```csharp
coven.Covenant()
    .Connect(discord)
    .Connect(agents)
    .StartupOrder(b => b
        .First<OpenAIDaemon>()
        .Then<DiscordDaemon>()
        .Parallel<SegmentationDaemon>())
    .Routes(c => ...);
```

**Pros**: Explicit, flexible  
**Cons**: More API surface, another thing to get wrong

**C. No Ordering Guarantees** — Start all daemons concurrently:

```csharp
await Task.WhenAll(daemons.Select(d => d.Start(ct)));
```

**Pros**: Simplest, fastest startup  
**Cons**: Doesn't handle true dependencies

### Recommendation: Option A (Declaration Order) as Default

Start daemons sequentially in manifest connection order. This is:

- Predictable
- Explicit enough (the user controls connection order)
- Simple to implement
- Easy to reason about

If we later discover that parallel startup or explicit ordering is needed, we can add Option B. But YAGNI — most daemons don't have hard startup dependencies.

---

## Integration with Covenants

### Flow of Daemon Information

```
┌──────────────────────────────────────────────────────────────────┐
│                         BUILD TIME                                │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  UseDiscordChat(config)  ──► BranchManifest                     │
│                              ├─ Produces: {ChatAfferent, ...}    │
│                              ├─ Consumes: {ChatEfferent, ...}    │
│                              └─ RequiredDaemons: [DiscordDaemon] │
│                                                                  │
│  UseOpenAIAgents(config) ──► BranchManifest                     │
│                              ├─ Produces: {AgentResponse, ...}   │
│                              ├─ Consumes: {AgentPrompt}          │
│                              └─ RequiredDaemons: [OpenAIDaemon]  │
│                                                                  │
│  Covenant()                                                      │
│    .Connect(discord)   ──► Collects DiscordDaemon               │
│    .Connect(agents)    ──► Collects OpenAIDaemon                │
│    .Routes(...)        ──► Validates routes                      │
│                                                                  │
│  Result: CovenantDescriptor                                      │
│    ├─ Routes: {...}                                              │
│    └─ RequiredDaemons: [DiscordDaemon, OpenAIDaemon] (ordered)   │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                         RUNTIME                                   │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Ritual<T, TOutput>(input, ct)                                   │
│    │                                                             │
│    ▼                                                             │
│  CovenExecutionScope.BeginScopeAsync(root, ct)                   │
│    │                                                             │
│    ├─► Create IServiceScope                                      │
│    │                                                             │
│    ├─► Resolve required daemons from CovenantDescriptor          │
│    │   foreach (Type daemonType in descriptor.RequiredDaemons)   │
│    │       daemons.Add(scope.GetRequiredService(daemonType))     │
│    │                                                             │
│    ├─► Start daemons in order                                    │
│    │   foreach (var daemon in daemons)                           │
│    │       await daemon.Start(ct)  // Fail fast on error         │
│    │                                                             │
│    └─► Return DaemonScope                                        │
│                                                                  │
│  ... ritual executes ...                                         │
│                                                                  │
│  DaemonScope.DisposeAsync()                                      │
│    │                                                             │
│    ├─► Shutdown daemons (reverse order)                          │
│    │   foreach (var daemon in daemons.Reverse())                 │
│    │       await daemon.Shutdown(ct)                             │
│    │                                                             │
│    └─► Dispose IServiceScope                                     │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Manifest Extension

Branch manifests declare their daemon requirements:

```csharp
public sealed record BranchManifest(
    string Name,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons)
{
    public static BranchManifest Create(
        string name,
        IEnumerable<Type> produces,
        IEnumerable<Type> consumes,
        params Type[] daemons) => new(
            name,
            produces.ToHashSet(),
            consumes.ToHashSet(),
            daemons.ToList());
}
```

Example branch registration:

```csharp
public static BranchManifest UseDiscordChat(
    this ICovenBuilder coven, 
    DiscordConfig config)
{
    // Register scoped services
    coven.Services.AddScoped<DiscordDaemon>();
    coven.Services.AddScoped<IScrivener<ChatEntry>, DiscordScrivener>();
    
    // Return manifest declaring what this branch provides
    return BranchManifest.Create(
        name: "DiscordChat",
        produces: [typeof(ChatAfferent)],
        consumes: [typeof(ChatEfferent), typeof(ChatEfferentDraft)],
        daemons: typeof(DiscordDaemon));
}
```

---

## Failure Semantics

### Scenario: Daemon B Fails to Start

```
Timeline:
  t0: BeginScopeAsync called
  t1: DaemonA.Start() succeeds → DaemonA is Running
  t2: DaemonB.Start() throws StartupFailedException
  
Question: What happens to DaemonA?
```

### Options

**A. Leave Running** — DaemonA stays running, scope creation fails

```csharp
try
{
    foreach (var daemon in daemons)
        await daemon.Start(ct);
}
catch
{
    // DaemonA is still running, scope.Dispose() will clean it up... maybe?
    throw;
}
```

**Problem**: Who shuts down DaemonA? The scope never finished creation, so the caller doesn't have a scope to dispose. DaemonA leaks.

**B. Stop All on Failure** — Roll back successfully started daemons

```csharp
var started = new List<ContractDaemon>();
try
{
    foreach (var daemon in daemons)
    {
        await daemon.Start(ct);
        started.Add(daemon);
    }
}
catch (Exception ex)
{
    // Roll back: stop daemons in reverse order
    foreach (var daemon in started.AsEnumerable().Reverse())
    {
        try { await daemon.Shutdown(CancellationToken.None); }
        catch { /* log, but continue rollback */ }
    }
    throw new DaemonStartupException(
        "Scope activation failed: daemon startup error", ex);
}
```

**Pros**: Clean state, no leaked daemons  
**Cons**: Slightly more complex

**C. Partial Success** — Return scope with partial daemons running

**Problem**: This defeats the purpose of fail-fast. The scope appears valid but is missing functionality.

### Recommendation: Option B (Stop All on Failure)

Scope activation is atomic with respect to daemons:
- Either all daemons start successfully and the scope is valid
- Or startup fails, all started daemons are stopped, and an exception propagates

This matches transactional semantics: commit all or rollback all.

### Exception Type

```csharp
public sealed class DaemonStartupException : Exception
{
    public DaemonStartupException(
        string message, 
        Exception innerException,
        IReadOnlyList<Type> failedDaemon,
        IReadOnlyList<Type> rolledBackDaemons)
        : base(message, innerException)
    {
        FailedDaemon = failedDaemon;
        RolledBackDaemons = rolledBackDaemons;
    }
    
    public IReadOnlyList<Type> FailedDaemon { get; }
    public IReadOnlyList<Type> RolledBackDaemons { get; }
}
```

---

## API Sketch

### Registration

```csharp
builder.Services.AddCoven(coven =>
{
    var chat = coven.UseDiscordChat(discordConfig);
    var agents = coven.UseOpenAIAgents(agentConfig);
    
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            c.Route<ChatAfferent, AgentPrompt>(...);
            c.Route<AgentResponse, ChatEfferentDraft>(...);
            c.Terminal<AgentThought>();
        });
    
    // Daemons are auto-started based on connected manifests
    // No explicit daemon configuration needed
});
```

### What Changes

| Component | Before | After |
|-----------|--------|-------|
| `CovenExecutionScope.BeginScope` | Sync, returns `IServiceScope` | Async, returns `DaemonScope` |
| `CovenExecutionScope.EndScope` | Sync, just disposes | Async, shuts down daemons then disposes |
| `BranchManifest` | `Produces`, `Consumes` | + `RequiredDaemons` |
| `ICoven.Ritual` | Sync scope creation | Async scope creation (internal change) |
| `IMagikBlock` implementations | Must start daemons manually | No daemon code needed |

### Block Simplification

Before:

```csharp
internal sealed class RouterBlock(
    IEnumerable<ContractDaemon> daemons,
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
    {
        // BOILERPLATE: Start daemons
        foreach (ContractDaemon d in _daemons)
            await d.Start(ct);
        
        // Actual logic...
    }
}
```

After:

```csharp
internal sealed class RouterBlock(
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
    {
        // Daemons already running — just do the work
        // Actual logic...
    }
}
```

Or, with declarative covenants, no `RouterBlock` at all.

---

## Build-Time Validation

The covenant validates daemon availability at registration:

```csharp
// Validation errors
✗ DiscordDaemon is required by branch "DiscordChat" but not registered.
  Ensure UseDiscordChat registers services correctly.

✗ OpenAIDaemon is declared in manifest but registered as Transient.
  Daemons must be Scoped. Change to: services.AddScoped<OpenAIDaemon>()
```

### Daemon Lifetime Constraints

Daemons **must** be scoped:

| Lifetime | Valid? | Reason |
|----------|--------|--------|
| Scoped | ✅ | One daemon instance per ritual scope |
| Transient | ❌ | Multiple instances would be started independently |
| Singleton | ❌ | Would be shared across rituals, lifecycle confusion |

The covenant builder validates this at registration time:

```csharp
foreach (var daemonType in manifest.RequiredDaemons)
{
    var descriptor = services.FirstOrDefault(
        d => d.ServiceType == daemonType);
    
    if (descriptor is null)
        throw new InvalidOperationException(
            $"{daemonType.Name} is required but not registered.");
    
    if (descriptor.Lifetime != ServiceLifetime.Scoped)
        throw new InvalidOperationException(
            $"{daemonType.Name} must be Scoped, not {descriptor.Lifetime}.");
}
```

---

## Implementation Phases

### Phase 1: Core Infrastructure

1. Add `RequiredDaemons` to `BranchManifest`
2. Modify `CovenExecutionScope` to support async entry
3. Implement daemon startup/shutdown in scope lifecycle
4. Add `DaemonStartupException` with rollback semantics

### Phase 2: Covenant Integration

1. Covenant builder collects daemon requirements from manifests
2. Validate daemon registration and lifetime at build time
3. Store ordered daemon list in `CovenantDescriptor`

### Phase 3: Migration Support

1. Deprecate `IEnumerable<ContractDaemon>` injection pattern
2. Add analyzer warning for manual daemon startup in blocks
3. Update existing blocks to remove daemon boilerplate

---

## Alternatives Considered

### IHostedService Pattern

Could daemons implement `IHostedService` and be started by the host?

**Problems**:
- `IHostedService` is singleton-scoped — starts once at app start
- Daemons need to be scoped per ritual
- Host lifecycle doesn't match ritual lifecycle

### Lazy<IDaemon> Resolution

Could we use `Lazy<T>` to delay daemon creation until first use?

**Problems**:
- Daemons need explicit `Start()` call, not just resolution
- Doesn't solve the "forget to start" problem — moves it to "forget to access"

### Aspect-Oriented Startup

Could we use AOP to intercept block execution and start daemons?

**Problems**:
- Complex runtime weaving
- Harder to debug
- Doesn't provide clean shutdown semantics

---

## Related Proposals

- **[declarative-covenants.md](declarative-covenants.md)** — Defines the covenant and manifest structures this proposal extends
- **[daemon-magistrate.md](daemon-magistrate.md)** — Monitors daemons for transient failures *after* successful startup; this proposal handles startup, magistrate handles runtime
