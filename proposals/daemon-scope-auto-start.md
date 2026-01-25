# Daemon Scope Auto-Start

> **Status**: Implemented  
> **Created**: 2026-01-24  
> **Implemented**: 2026-01-24  
> **Depends on**: [declarative-covenants.md](declarative-covenants.md), [idaemon-to-core.md](idaemon-to-core.md)

---

## Problem

Manual daemon startup is error-prone boilerplate that plagues every block in the codebase:

```csharp
// This pattern appears in EVERY block that uses daemons
public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
{
    // Boilerplate: start all daemons
    foreach (IDaemon d in _daemons)
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
- **Parallel startup or explicit dependency ordering** — Declaration order is sufficient; advanced orchestration is out of scope

---

## Design

### Async Scope Entry

`CovenExecutionScope.BeginScopeAsync` creates the scope AND starts daemons before returning:

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
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        var daemons = scope.ServiceProvider
            .GetServices<IDaemon>()
            .ToList();
        
        // Start daemons as part of scope entry
        await StartDaemonsInOrderAsync(daemons, cts.Token);
        
        var daemonScope = new DaemonScope(scope, daemons, cts);
        _currentScope.Value = daemonScope;
        return daemonScope;
    }
    
    internal static async Task EndScopeAsync(DaemonScope? scope, CancellationToken ct)
    {
        if (scope is null) return;
        
        try
        {
            // Cancel the scope's CTS first — triggers cooperative shutdown
            // in daemon loops before we call Shutdown()
            await scope.Cts.CancelAsync();
            
            // Now formally shutdown daemons in reverse startup order
            await ShutdownDaemonsAsync(scope.Daemons.Reverse(), ct);
        }
        finally
        {
            scope.Cts.Dispose();
            scope.Scope.Dispose();
            _currentScope.Value = null;
        }
    }
}

internal sealed record DaemonScope(
    IServiceScope Scope, 
    IReadOnlyList<IDaemon> Daemons,
    CancellationTokenSource Cts) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await CovenExecutionScope.EndScopeAsync(this, CancellationToken.None);
    }
}
```

This approach fits naturally with the existing `CovenExecutionScope` pattern and changes `BeginScope` to an async signature.

#### Why Cooperative Cancellation Works

The scope's `CancellationTokenSource` enables graceful shutdown because daemons internally use `CreateLinkedTokenSource` to link their processing loops to the token passed during `Start()`. When the scope cancels its CTS:

1. **Daemon loops observe cancellation** — Any `await` checking the token exits cleanly
2. **Work-in-progress completes or aborts** — Depending on daemon implementation
3. **`Shutdown()` finds daemons already winding down** — Making formal shutdown fast

This two-phase approach (cancel CTS, then call `Shutdown()`) ensures daemons don't block indefinitely on pending work during disposal. The `CancellationToken.None` passed to `DisposeAsync` → `EndScopeAsync` is intentional: once we've decided to dispose, we should complete disposal regardless of external cancellation requests.

### Covenant-Driven Daemon Discovery

The covenant declares which daemons are needed based on connected manifests:

```csharp
public sealed record BranchManifest(
    string Name,
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);  // Daemons this branch needs
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

When you `.Connect(manifest)` to a covenant, the manifest's daemon requirements bubble up. The covenant collects all required daemons from all connected manifests and validates them at build time.

### Isolation Principle

Branches are isolation boundaries. Each branch owns its daemons exclusively:

- **Branch-scoped daemons** — Each branch's daemons are scoped to that branch's subgraph
- **No accidental sharing** — OpenAI and Discord never share daemons because they're separate subgraphs
- **Routing-only communication** — Cross-branch communication happens only through the Covenant's routing layer

This isolation means you cannot accidentally share a daemon between Discord and OpenAI. They have separate scopes. If a Discord branch needs to communicate with an OpenAI branch, that communication flows through explicitly declared routes in the covenant, not through shared daemon state.

```
┌─────────────────────────────────────────────────────────────────┐
│                          COVENANT                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐          ┌─────────────────────┐      │
│  │   Discord Branch    │          │   OpenAI Branch     │      │
│  │                     │          │                     │      │
│  │  ┌───────────────┐  │          │  ┌───────────────┐  │      │
│  │  │DiscordDaemon  │  │  routes  │  │ OpenAIDaemon  │  │      │
│  │  │(scoped here)  │  │◄────────►│  │(scoped here)  │  │      │
│  │  └───────────────┘  │          │  └───────────────┘  │      │
│  │                     │          │                     │      │
│  └─────────────────────┘          └─────────────────────┘      │
│          ▲                                  ▲                   │
│          │                                  │                   │
│    isolation boundary               isolation boundary          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

This design ensures that daemon lifecycle, state, and failures are contained within their branch. A problem in the Discord daemon cannot directly corrupt the OpenAI daemon's state — the worst case is that messages stop flowing through the routes.

---

## Daemon Startup Ordering

### The Problem

Daemons may have startup dependencies:

```
DiscordDaemon ──────► needs connection before...
OpenAIDaemon ──────► can start independently
SegmentationDaemon ► needs OpenAIDaemon's model loaded first
```

### Declaration Order

Daemons start sequentially in manifest connection order:

```csharp
coven.Covenant()
    .Connect(discord)   // DiscordDaemon starts first
    .Connect(agents)    // OpenAIDaemon starts second
    .Routes(c => ...);
```

This approach is:

- **Predictable** — The user controls connection order explicitly
- **Simple to implement** — Sequential startup in declared order
- **Easy to reason about** — What you see is what you get

Declaration order provides deterministic, explicit control over daemon startup sequence. Developers who need a specific startup order simply declare their branches in that order.

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
│  Result: Internal covenant configuration                         │
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
│    ├─► Create CancellationTokenSource (linked to caller's ct)    │
│    │                                                             │
│    ├─► Resolve required daemons from covenant configuration     │
│    │   foreach (Type daemonType in config.RequiredDaemons)       │
│    │       daemons.Add(scope.GetRequiredService(daemonType))     │
│    │                                                             │
│    ├─► Start daemons in order (passing cts.Token)                │
│    │   foreach (var daemon in daemons)                           │
│    │       await daemon.Start(cts.Token)  // Fail fast on error  │
│    │                                                             │
│    └─► Return DaemonScope (with CTS)                             │
│                                                                  │
│  ... ritual executes ...                                         │
│                                                                  │
│  DaemonScope.DisposeAsync()                                      │
│    │                                                             │
│    ├─► Cancel scope's CTS (triggers cooperative shutdown)        │
│    ├─► Shutdown daemons (reverse order)                          │
│    │   foreach (var daemon in daemons.Reverse())                 │
│    │       await daemon.Shutdown(ct)                             │
│    ├─► Dispose CTS                                               │
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

### Atomic Rollback

Scope activation is atomic with respect to daemons. If any daemon fails to start, all successfully started daemons are rolled back:

```csharp
var started = new List<IDaemon>();
try
{
    foreach (var daemon in daemons)
    {
        await daemon.Start(cts.Token);
        started.Add(daemon);
    }
}
catch (Exception ex)
{
    // Cancel the CTS first — signals daemons to stop cooperatively
    await cts.CancelAsync();
    
    // Roll back: stop daemons in reverse order
    // Use CancellationToken.None here — we're already in error state
    // and need to complete rollback regardless of external cancellation
    foreach (var daemon in started.AsEnumerable().Reverse())
    {
        try { await daemon.Shutdown(CancellationToken.None); }
        catch { /* log, but continue rollback */ }
    }
    
    var failedDaemon = daemons[started.Count].GetType();
    var rolledBack = started.Select(d => d.GetType()).ToList();
    throw new DaemonStartupException(
        "Scope activation failed: daemon startup error", 
        ex,
        failedDaemon,
        rolledBack);
}
```

This guarantees:

- **Either all daemons start successfully** and the scope is valid
- **Or startup fails**, all started daemons are stopped, and an exception propagates

No leaked daemons, no partial states. This matches transactional semantics: commit all or rollback all.

### Exception Type

```csharp
public sealed class DaemonStartupException : Exception
{
    public DaemonStartupException(
        string message, 
        Exception innerException,
        Type failedDaemon,
        IReadOnlyList<Type> rolledBackDaemons)
        : base(message, innerException)
    {
        FailedDaemon = failedDaemon;
        RolledBackDaemons = rolledBackDaemons;
    }
    
    /// <summary>The daemon type that failed to start.</summary>
    public Type FailedDaemon { get; }
    
    /// <summary>Daemon types that were successfully started and then rolled back.</summary>
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
    IEnumerable<IDaemon> daemons,
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
    {
        // BOILERPLATE: Start daemons
        foreach (IDaemon d in _daemons)
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
3. Store ordered daemon list in internal covenant configuration

### Phase 3: Migration Support

1. Deprecate `IEnumerable<IDaemon>` injection pattern
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
