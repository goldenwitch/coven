# Proposal: CovenantAdherentDaemon

## Status
Implemented

**Documentation**: [architecture/Covenants-and-Routing.md](../architecture/Covenants-and-Routing.md)

## Summary

Introduce `CovenantAdherentDaemon`, a daemon that executes covenant routes at runtime by tailing source journals, applying route transformations, and writing results to target journals. This replaces the need for user-written `RouterBlock` boilerplate.

## Motivation

The declarative covenants proposal defines a build-time API for declaring routes between branches:

```csharp
coven.Covenant()
    .Connect(chat)
    .Connect(agents)
    .Routes(c =>
    {
        c.Route<ChatAfferent, AgentPrompt>((msg, ct) => ...);
        c.Route<AgentResponse, ChatEfferentDraft>((r, ct) => ...);
        c.Terminal<AgentThought>();
    });
```

However, the proposal leaves runtime execution unspecified. Currently, users must write imperative `RouterBlock` classes that manually:
- Tail journals via `IScrivener<T>.TailAsync()`
- Pattern match entry types
- Transform and write to target journals

This is exactly the boilerplate covenants should eliminate.

## Design

### CovenantAdherentDaemon

A single daemon responsible for executing all routes defined in a covenant.

```csharp
internal sealed class CovenantAdherentDaemon : IDaemon
{
    private readonly CovenantDescriptor _covenant;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    public Status Status { get; private set; } = Status.Stopped;

    public CovenantAdherentDaemon(
        CovenantDescriptor covenant,
        IServiceProvider services)
    {
        _covenant = covenant;
        _services = services;
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = RunPumpsAsync(_cts.Token);
        Status = Status.Running;
    }

    public async Task Shutdown(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_pumpTask is not null)
                await _pumpTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        Status = Status.Completed;
    }
}
```

### Route Pump Architecture

For each route in the covenant, spawn a **pump**—a long-running task that tails a source journal, transforms matching entries, and writes results to a target journal.

```csharp
private async Task RunPumpsAsync(CancellationToken ct)
{
    var tasks = _covenant.Pumps.Select(pump => pump.CreatePump(_services, ct));
    await Task.WhenAll(tasks);
}
```

Each pump is a pre-built closure containing fully-typed scrivener access (see "Route Invocation Design" below for construction details).

**Concurrency model**: Each route runs an independent pump. This maximizes isolation but means multiple pumps may tail the same source journal with independent cursors.

### Scrivener Resolution

**Problem**: Routes specify leaf entry types (`ChatAfferent`), but scriveners are registered for base journal types (`IScrivener<ChatEntry>`). The daemon needs to map from leaf types to journal types.

**Solution**: Extend `BranchManifest` to declare its journal entry type explicitly:

```csharp
public sealed record BranchManifest(
    string Name,
    Type JournalEntryType,              // NEW: e.g., typeof(ChatEntry)
    IReadOnlySet<Type> Produces,
    IReadOnlySet<Type> Consumes,
    IReadOnlyList<Type> RequiredDaemons);
```

Branch extensions then declare this at design time:

```csharp
// In UseDiscord()
return new BranchManifest(
    Name: "DiscordChat",
    JournalEntryType: typeof(ChatEntry),
    Produces: [typeof(ChatAfferent)],
    Consumes: [typeof(ChatEfferent), typeof(ChatEfferentDraft)],
    RequiredDaemons: [typeof(ContractDaemon)]);
```

**Benefits**:
- Build-time validation can verify `Produces`/`Consumes` types derive from `JournalEntryType`
- Manifest fully describes its journal relationship
- At registration time, we can build a dictionary from leaf types to journal types and construct fully-typed pump closures (see "Route Invocation Design" below)

### Source Type Filtering

A scrivener for `ChatEntry` yields all subtypes (`ChatAfferent`, `ChatEfferentDraft`, etc.). The pump filters to only process entries matching the route's source type via exact type comparison: `entry.GetType() == route.SourceType`.

### Terminal Handling

Terminal types are not pumped—they have no route by design. The daemon only creates pumps for `RouteDescriptor` entries; `TerminalDescriptor` entries are ignored. Coverage validation (ensuring every produced type has a route or terminal) is enforced at build time by `CovenantBuilder`.

## Registration

The daemon and its descriptor are registered during covenant building in `CovenantBuilder.Routes()`, after validation:

```csharp
// Build entry-to-journal lookup from manifests
Dictionary<Type, Type> entryToJournal = _manifests
    .SelectMany(m => m.Produces.Concat(m.Consumes)
        .Select(t => (Entry: t, Journal: m.JournalEntryType)))
    .ToDictionary(x => x.Entry, x => x.Journal);

// Build typed pumps for each route (one-time reflection per route)
List<PumpDescriptor> pumps = definition.Routes
    .Select(route => CreateTypedPump(route, entryToJournal))
    .ToList();

// Create and register the covenant descriptor (scoped)
var descriptor = new CovenantDescriptor([.. _manifests], pumps);
_services.AddScoped(_ => descriptor);

// Register the covenant adherent daemon
_services.AddScoped<IDaemon, CovenantAdherentDaemon>();

// Then call existing RegisterDaemons() for branch daemons
```

**Why scoped for both?**
- `CovenantDescriptor`: Different `DaemonScope` instances may operate under different covenants (e.g., test vs production routes).
- `CovenantAdherentDaemon`: Daemons are tied to scope lifetime—each scope gets fresh daemon instances.

The daemon receives the descriptor via constructor injection:

```csharp
public CovenantAdherentDaemon(
    CovenantDescriptor covenant,
    IServiceProvider services)
```

It will be auto-started by `DaemonScope.BeginScopeAsync()` alongside other daemons.

## Future Work: Compute Wrappers

This proposal implements pure routing. Routes execute transformations directly with no interception points. A future proposal may introduce compute wrappers for cross-cutting concerns (logging, metrics, caching):

```csharp
// Future API (not this proposal)
c.Route<A, B>(transform)
    .WithCompute(async (input, next, ct) =>
    {
        Log.Information("Processing {Type}", input.GetType());
        return await next(input, ct);
    });
```

## Open Questions

None. See decisions below.

## Decisions

**Error handling**: Pure transformations have no transient failures—they either succeed or fail due to bugs/bad data. Retrying won't help. If a transformation throws, fail the daemon. This surfaces bugs immediately and aligns with fail-fast philosophy.

**Position tracking**: Deferred to a separate proposal. Snapshotting and resume behavior is orthogonal to routing. Scriveners are scoped to the daemon scope, so any persistence mechanism must be decoupled from both the daemon and scrivener lifetimes.

**Concurrency model**: One pump per route, maximizing isolation. Multiple pumps may tail the same source journal with independent cursors.

**Entry constraints on ICovenant**: The `Route<TSource, TTarget>` methods add `where TSource : Entry` and `where TTarget : Entry` constraints. This is a breaking change to the public `ICovenant` interface, but benign—all real uses involve Entry-derived types. The constraint enables type-erased invokers without falling back to `object`.

**BranchManifest.JournalEntryType**: Adding a new required parameter to the public `BranchManifest` record is a breaking change. All existing branch registrations (UseDiscordChat, UseOpenAIAgents, etc.) must be updated. This is acceptable as the codebase is pre-1.0.

## Route Invocation Design

### Two-Phase Type Capture

Type information is available at two distinct moments:

1. **Route definition time** (`c.Route<TSource, TTarget>(...)`): We know leaf entry types and can capture the transformation in a typed closure.

2. **Covenant registration time** (`Routes()` completes): We have all manifests, so we know which `JournalEntryType` each leaf type belongs to. We can build fully-typed pump closures.

The strategy: capture what we can at each phase, deferring nothing to runtime.

### Phase 1: Route Definition

At definition time, capture a type-erased invoker. All journal entries derive from `Entry` ([Entry.cs](../src/Coven.Core/Entry.cs)), so we can narrow to that rather than `object`.

#### Lambda Routes

```csharp
// In CovenantDefinition.Route<TSource, TTarget>()
public ICovenant Route<TSource, TTarget>(Func<TSource, CancellationToken, Task<TTarget>> map)
    where TSource : Entry
    where TTarget : Entry
{
    // Capture transformation in closure—no reflection at invocation
    Func<Entry, CancellationToken, Task<Entry>> invoker = async (entry, ct) =>
    {
        return await map((TSource)entry, ct);
    };
    
    _routes.Add(new LambdaRouteDescriptor(typeof(TSource), typeof(TTarget), invoker));
    return this;
}
```

#### Transmuter Routes

For transmuters, defer resolution until we have a service provider:

```csharp
// In CovenantDefinition.Route<TSource, TTarget, TTransmuter>()
public ICovenant Route<TSource, TTarget, TTransmuter>()
    where TSource : Entry
    where TTarget : Entry
    where TTransmuter : class, ITransmuter<TSource, TTarget>
{
    Func<IServiceProvider, Func<Entry, CancellationToken, Task<Entry>>> createInvoker = sp =>
    {
        var transmuter = sp.GetRequiredService<TTransmuter>();
        return async (entry, ct) =>
        {
            return await transmuter.Transmute((TSource)entry, ct);
        };
    };
    
    _routes.Add(new TransmuterRouteDescriptor(
        typeof(TSource), typeof(TTarget), typeof(TTransmuter), createInvoker));
    return this;
}
```

#### Route Descriptors

```csharp
internal abstract record RouteDescriptor(Type SourceType, Type TargetType);

internal sealed record LambdaRouteDescriptor(
    Type SourceType,
    Type TargetType,
    Func<Entry, CancellationToken, Task<Entry>> Invoke)
    : RouteDescriptor(SourceType, TargetType);

internal sealed record TransmuterRouteDescriptor(
    Type SourceType,
    Type TargetType,
    Type TransmuterType,
    Func<IServiceProvider, Func<Entry, CancellationToken, Task<Entry>>> CreateInvoker)
    : RouteDescriptor(SourceType, TargetType);
```

### Phase 2: Pump Construction

When `Routes()` completes, we have manifests (with `JournalEntryType`) and routes (with invokers). Now we build **fully-typed pump factories** that capture scrivener types in closures.

#### PumpDescriptor

```csharp
/// <summary>
/// A pre-compiled pump that can be executed with a service provider.
/// All type information is captured in the factory closure.
/// </summary>
internal sealed record PumpDescriptor(
    Type SourceType,
    Type TargetType,
    Func<IServiceProvider, CancellationToken, Task> CreatePump);
```

#### Building Pumps at Registration Time

In `CovenantBuilder.Routes()`, after validation:

```csharp
// Build entry-to-journal lookup from manifests
Dictionary<Type, Type> entryToJournal = _manifests
    .SelectMany(m => m.Produces.Concat(m.Consumes)
        .Select(t => (Entry: t, Journal: m.JournalEntryType)))
    .ToDictionary(x => x.Entry, x => x.Journal);

// Build typed pumps for each route
List<PumpDescriptor> pumps = [];
foreach (RouteDescriptor route in definition.Routes)
{
    Type sourceJournal = entryToJournal[route.SourceType];
    Type targetJournal = entryToJournal[route.TargetType];
    
    PumpDescriptor pump = CreateTypedPump(route, sourceJournal, targetJournal);
    pumps.Add(pump);
}

// Register descriptor with pre-built pumps
var descriptor = new CovenantDescriptor([.. _manifests], pumps);
_services.AddScoped(_ => descriptor);
```

#### Generic Pump Factory

The `CreateTypedPump` method uses reflection **once** to invoke a generic helper:

```csharp
private static readonly MethodInfo s_buildPumpMethod = 
    typeof(CovenantBuilder).GetMethod(nameof(BuildPump), BindingFlags.NonPublic | BindingFlags.Static)!;

private static PumpDescriptor CreateTypedPump(
    RouteDescriptor route, 
    Type sourceJournal, 
    Type targetJournal)
{
    // One-time reflection to call BuildPump<TSourceJournal, TTargetJournal>
    MethodInfo generic = s_buildPumpMethod.MakeGenericMethod(sourceJournal, targetJournal);
    return (PumpDescriptor)generic.Invoke(null, [route])!;
}

private static PumpDescriptor BuildPump<TSourceJournal, TTargetJournal>(RouteDescriptor route)
    where TSourceJournal : Entry
    where TTargetJournal : Entry
{
    // Extract the invoker (handles both lambda and transmuter routes)
    Func<IServiceProvider, Func<Entry, CancellationToken, Task<Entry>>> getInvoker = route switch
    {
        LambdaRouteDescriptor lambda => _ => lambda.Invoke,
        TransmuterRouteDescriptor transmuter => transmuter.CreateInvoker,
        _ => throw new InvalidOperationException($"Unknown route type: {route.GetType().Name}")
    };
    
    // Return a pump factory with fully-typed scrivener access
    return new PumpDescriptor(
        route.SourceType,
        route.TargetType,
        (sp, ct) => RunPumpAsync<TSourceJournal, TTargetJournal>(
            sp, route.SourceType, getInvoker(sp), ct));
}

private static async Task RunPumpAsync<TSourceJournal, TTargetJournal>(
    IServiceProvider sp,
    Type sourceLeafType,
    Func<Entry, CancellationToken, Task<Entry>> invoke,
    CancellationToken ct)
    where TSourceJournal : Entry
    where TTargetJournal : Entry
{
    // Fully typed—no reflection, no dynamic dispatch
    var source = sp.GetRequiredService<IScrivener<TSourceJournal>>();
    var target = sp.GetRequiredService<IScrivener<TTargetJournal>>();
    
    await foreach ((long _, TSourceJournal entry) in source.TailAsync(0, ct))
    {
        if (entry.GetType() != sourceLeafType)
            continue;
            
        Entry result = await invoke(entry, ct);
        await target.WriteAsync((TTargetJournal)result, ct);
    }
}
```

### Updated CovenantDescriptor

```csharp
internal sealed record CovenantDescriptor(
    IReadOnlyList<BranchManifest> Manifests,
    IReadOnlyList<PumpDescriptor> Pumps);  // Pre-built pumps, not raw routes
```

### Simplified Daemon

The daemon becomes trivial—just execute pre-built pumps:

```csharp
private async Task RunPumpsAsync(CancellationToken ct)
{
    var tasks = _covenant.Pumps.Select(pump => pump.CreatePump(_services, ct));
    await Task.WhenAll(tasks);
}
```

No type resolution. No reflection. The pump closures contain fully-typed code.

### Why This Works

1. **Route definition captures transformation types** in closures (`TSource`, `TTarget`)
2. **Registration captures journal types** in closures (`TSourceJournal`, `TTargetJournal`)
3. **Reflection happens once** at registration—`MakeGenericMethod` builds typed pump factories
4. **Runtime is pure execution**—closures contain fully-typed `IScrivener<T>` access
5. **Casts are verifiable**—`TTargetJournal` constrains what `WriteAsync` accepts; if the route produces the wrong type, it fails fast with `InvalidCastException`

## Status

Pending review.
