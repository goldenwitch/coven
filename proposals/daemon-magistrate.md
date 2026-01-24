# Daemon Magistrate

> **Status**: Proposal  
> **Created**: 2026-01-24

---

## Problem

Daemons in Coven follow a clear lifecycle: `Stopped → Running → Completed`. The current `ContractDaemon` base class provides:

- **Status transitions** via `Transition()` — journaled and thread-safe
- **Failure reporting** via `Fail()` — surfaces the first exception to observers
- **Waitable promises** via `WaitFor()` and `WaitForFailure()`

This works well for **startup failures** — orchestration code can wait for `Running` and race against `WaitForFailure()` to fail fast if a daemon cannot start.

But what happens when a daemon **successfully starts** and then encounters transient failures during operation?

### The Gap

```
                    ┌─────────────────────────────────────────┐
                    │        TRANSIENT FAILURE ZONE           │
                    │                                         │
   Stopped ──► Running ──────────────────────────────► Completed
                 ▲                                         │
                 │         Network timeout                 │
                 │         Lock contention                 │
                 │         Memory pressure                 │
                 │         Rate limiting                   │
                 │         Upstream service hiccup         │
                 │                                         │
                 └─────────── (currently unhandled) ───────┘
```

Today, a daemon encountering transient failures has limited options:

1. **Call `Fail(ex)`** — Surfaces the exception, but provides no recovery mechanism. Observers see the failure but must implement their own retry logic externally.

2. **Swallow and retry internally** — Each daemon implements its own retry logic, leading to inconsistent behavior, duplicated code, and no visibility into failure patterns.

3. **Crash** — Let the exception propagate, terminating the daemon. No recovery possible.

None of these options provide **supervised recovery** — a consistent, observable mechanism for detecting transient failures and attempting recovery before escalating.

### Real-World Scenarios

| Scenario | Transient Cause | Desired Behavior |
|----------|-----------------|------------------|
| Discord daemon loses WebSocket | Network blip | Reconnect with backoff |
| OpenAI agent hits rate limit | API throttling | Wait and retry |
| File scrivener can't write | Disk full / locked | Retry after delay |
| Stream processor falls behind | Memory pressure | Back-pressure, then recover |

---

## Naming

The component should supervise daemons, detect failures, and decide recovery strategies. Several names fit the Coven's arcane vocabulary:

| Name | Connotation | Fit |
|------|-------------|-----|
| **Magistrate** | Judicial overseer who passes judgment | Good — implies authority over daemon behavior |
| **Warden** | Guardian who watches over charges | Good — implies protective supervision |
| **Sentinel** | Watchful guardian, early warning | Good — implies vigilant monitoring |
| **Inquisitor** | Investigator who probes for problems | Darker tone — may imply intrusive inspection |
| **Arbiter** | Judge who decides disputes | Okay — less about ongoing supervision |
| **Vigil** | Watchful devotion | Poetic but unclear responsibility |
| **Shepherd** | Guides flock, returns strays | Good — implies recovery/herding behavior |
| **Overseer** | General supervisor | Generic, less mystical |

### Recommendation

**DaemonWarden** or **DaemonMagistrate** both work well.

- **Warden** emphasizes protection and recovery ("the warden keeps daemons safe")
- **Magistrate** emphasizes judgment and policy ("the magistrate decides what to do about failures")

This proposal uses **Magistrate** but the implementation could use either. The key insight is that the component *passes judgment* on failures — deciding whether they're transient (retry) or permanent (escalate).

---

## Responsibilities

### 1. Health Monitoring

Observe daemon status and detect anomalies:

```csharp
// Daemon emits health signals
public record DaemonHeartbeat(DateTimeOffset Timestamp) : DaemonEvent;
public record DaemonDistress(Exception Error, DistressSeverity Severity) : DaemonEvent;

public enum DistressSeverity
{
    Transient,   // Recoverable: network timeout, rate limit
    Degraded,    // Partial function: some operations failing
    Critical     // Needs intervention: persistent failures
}
```

Unlike `Fail()` which is terminal, `Distress` signals allow the daemon to communicate "I'm struggling but still running."

### 2. Failure Classification

Not all failures are equal. The magistrate must distinguish:

| Classification | Characteristics | Response |
|----------------|-----------------|----------|
| **Transient** | Temporary, external cause | Retry with backoff |
| **Degraded** | Partial functionality lost | Monitor, alert, possible restart |
| **Permanent** | Unrecoverable | Escalate, transition to Completed |

Classification can be:
- **Exception-based**: `TimeoutException` → transient, `ConfigurationException` → permanent
- **Pattern-based**: 3 consecutive transient failures → escalate to degraded
- **Policy-based**: Per-daemon configuration

### 3. Recovery Strategies

```csharp
public interface IRecoveryStrategy
{
    /// <summary>
    /// Determines whether recovery should be attempted.
    /// </summary>
    bool ShouldAttemptRecovery(DistressContext context);
    
    /// <summary>
    /// Returns the delay before the next recovery attempt.
    /// </summary>
    TimeSpan GetBackoffDelay(DistressContext context);
    
    /// <summary>
    /// Executes recovery. Returns true if recovery succeeded.
    /// </summary>
    Task<bool> AttemptRecoveryAsync(ContractDaemon daemon, CancellationToken ct);
}
```

Built-in strategies:

| Strategy | Behavior |
|----------|----------|
| **RestartStrategy** | Stop and restart the daemon |
| **BackoffStrategy** | Wait with exponential backoff, let daemon self-heal |
| **CircuitBreakerStrategy** | After N failures, stop attempting recovery for a cooldown period |
| **CompositeStrategy** | Chain strategies: backoff → restart → circuit breaker |

### 4. Escalation

When recovery fails or limits are exceeded:

```csharp
public interface IEscalationHandler
{
    /// <summary>
    /// Called when the magistrate gives up on a daemon.
    /// </summary>
    Task HandleEscalationAsync(
        ContractDaemon daemon,
        IReadOnlyList<DistressContext> failureHistory,
        CancellationToken ct);
}
```

Escalation might:
- Log a critical error
- Trigger application shutdown
- Notify external monitoring
- Attempt to gracefully degrade the system

---

## Design Considerations

### Opt-in vs Default

**Recommendation: Opt-in with easy enablement.**

```csharp
// Opt-in per daemon
services.AddDaemonMagistrate(magistrate =>
{
    magistrate.Supervise<DiscordChatDaemon>(policy => 
    {
        policy.OnTransient(new ExponentialBackoff(
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromMinutes(5),
            maxAttempts: 10));
        
        policy.OnDegraded(new RestartWithCooldown(
            cooldown: TimeSpan.FromMinutes(1)));
        
        policy.OnEscalation(ctx => ctx.RequestShutdown());
    });
});

// Or supervise all daemons with a default policy
services.AddDaemonMagistrate(magistrate =>
{
    magistrate.SuperviseAll(DefaultRecoveryPolicy.Resilient);
});
```

**Rationale**: Not all daemons need supervision. Some are expected to complete (batch processors). Some have their own recovery logic. Making it opt-in avoids surprises while making common cases easy.

### Per-Daemon vs Global Policies

**Recommendation: Both, with per-daemon override.**

```csharp
services.AddDaemonMagistrate(magistrate =>
{
    // Global default
    magistrate.DefaultPolicy = new RecoveryPolicy
    {
        MaxTransientRetries = 5,
        BackoffMultiplier = 2.0,
        CircuitBreakerThreshold = 3
    };
    
    // Per-daemon override
    magistrate.Supervise<DiscordChatDaemon>(policy =>
    {
        // Discord has its own reconnection logic, be more patient
        policy.MaxTransientRetries = 20;
        policy.BackoffMultiplier = 1.5;
    });
    
    // Some daemons shouldn't be supervised
    magistrate.Ignore<BatchProcessorDaemon>();
});
```

### Interaction with Cancellation

The magistrate must respect the application's cancellation token:

```csharp
public class DaemonMagistrate : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Start monitoring supervised daemons
        foreach (var supervised in _supervisedDaemons)
        {
            _ = MonitorDaemonAsync(supervised, ct);
        }
    }
    
    private async Task MonitorDaemonAsync(SupervisedDaemon supervised, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var distress = await supervised.Daemon.WaitForDistress(ct);
            
            if (ct.IsCancellationRequested) break;
            
            await HandleDistressAsync(supervised, distress, ct);
        }
    }
}
```

**Key rule**: When the application is shutting down (`ct.IsCancellationRequested`), the magistrate should **not** attempt recovery. It should allow daemons to complete their shutdown gracefully.

### Expected-to-Complete Daemons

Some daemons are designed to complete (batch jobs, one-shot processors). The magistrate should handle this:

```csharp
magistrate.Supervise<BatchImportDaemon>(policy =>
{
    // This daemon is expected to complete
    policy.ExpectedLifetime = DaemonLifetime.Bounded;
    
    // Don't treat completion as failure
    policy.TreatCompletionAs = CompletionBehavior.Success;
    
    // But DO supervise while it's running
    policy.OnTransient(new ExponentialBackoff(...));
});
```

vs.

```csharp
magistrate.Supervise<DiscordChatDaemon>(policy =>
{
    // This daemon should run forever
    policy.ExpectedLifetime = DaemonLifetime.Unbounded;
    
    // Treat unexpected completion as failure
    policy.TreatCompletionAs = CompletionBehavior.Failure;
});
```

---

## API Sketch

### New DaemonEvent Types

```csharp
// In Coven.Daemonology

[JsonDerivedType(typeof(Heartbeat), nameof(Heartbeat)),
 JsonDerivedType(typeof(Distress), nameof(Distress)),
 JsonDerivedType(typeof(RecoveryAttempted), nameof(RecoveryAttempted)),
 JsonDerivedType(typeof(RecoverySucceeded), nameof(RecoverySucceeded))]
public abstract record DaemonEvent;

// Existing
internal sealed record StatusChanged(Status NewStatus) : DaemonEvent;
internal sealed record FailureOccurred(Exception Exception) : DaemonEvent;

// New
public sealed record Heartbeat(DateTimeOffset Timestamp) : DaemonEvent;
public sealed record Distress(
    Exception Error, 
    DistressSeverity Severity,
    DateTimeOffset Timestamp) : DaemonEvent;
public sealed record RecoveryAttempted(
    int AttemptNumber,
    string Strategy,
    DateTimeOffset Timestamp) : DaemonEvent;
public sealed record RecoverySucceeded(
    int AttemptsRequired,
    TimeSpan TotalDowntime,
    DateTimeOffset Timestamp) : DaemonEvent;
```

### ContractDaemon Extensions

```csharp
public abstract class ContractDaemon
{
    // Existing
    protected async Task Fail(Exception error, CancellationToken ct);
    
    // New: Signal distress without terminal failure
    protected async Task SignalDistress(
        Exception error, 
        DistressSeverity severity = DistressSeverity.Transient,
        CancellationToken ct = default)
    {
        await _scrivener.WriteAsync(
            new Distress(error, severity, DateTimeOffset.UtcNow), 
            ct);
    }
    
    // New: Emit heartbeat for health monitoring
    protected async Task Heartbeat(CancellationToken ct = default)
    {
        await _scrivener.WriteAsync(
            new Heartbeat(DateTimeOffset.UtcNow), 
            ct);
    }
    
    // New: Wait for distress (for magistrate)
    public Task<Distress> WaitForDistress(CancellationToken ct = default)
        => _scrivener.WaitForAsync<Distress>(0, _ => true, ct)
            .ContinueWith(t => t.Result.entry, ct);
}
```

### Magistrate Configuration

```csharp
// In Coven.Daemonology.Magistrate (new assembly or namespace)

public static class MagistrateServiceCollectionExtensions
{
    public static IServiceCollection AddDaemonMagistrate(
        this IServiceCollection services,
        Action<MagistrateBuilder> configure)
    {
        var builder = new MagistrateBuilder(services);
        configure(builder);
        
        services.AddHostedService<DaemonMagistrate>();
        return services;
    }
}

public class MagistrateBuilder
{
    public RecoveryPolicy DefaultPolicy { get; set; } = RecoveryPolicy.Default;
    
    public MagistrateBuilder Supervise<TDaemon>(
        Action<DaemonPolicyBuilder> configure) 
        where TDaemon : ContractDaemon;
    
    public MagistrateBuilder SuperviseAll(RecoveryPolicy policy);
    
    public MagistrateBuilder Ignore<TDaemon>() 
        where TDaemon : ContractDaemon;
    
    public MagistrateBuilder OnEscalation(IEscalationHandler handler);
}

public class DaemonPolicyBuilder
{
    public DaemonLifetime ExpectedLifetime { get; set; }
    public CompletionBehavior TreatCompletionAs { get; set; }
    
    public DaemonPolicyBuilder OnTransient(IRecoveryStrategy strategy);
    public DaemonPolicyBuilder OnDegraded(IRecoveryStrategy strategy);
    public DaemonPolicyBuilder OnCritical(IRecoveryStrategy strategy);
}
```

### Built-in Policies

```csharp
public static class RecoveryPolicy
{
    /// <summary>
    /// No recovery attempts. Failures are logged and escalated immediately.
    /// </summary>
    public static readonly RecoveryPolicy None = new()
    {
        TransientStrategy = NoRecoveryStrategy.Instance,
        DegradedStrategy = NoRecoveryStrategy.Instance
    };
    
    /// <summary>
    /// Sensible defaults: exponential backoff for transients, restart for degraded.
    /// </summary>
    public static readonly RecoveryPolicy Default = new()
    {
        TransientStrategy = new ExponentialBackoffStrategy(
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromMinutes(2),
            maxAttempts: 5),
        DegradedStrategy = new RestartStrategy(maxAttempts: 2)
    };
    
    /// <summary>
    /// Aggressive recovery: more retries, longer backoff, circuit breaker.
    /// </summary>
    public static readonly RecoveryPolicy Resilient = new()
    {
        TransientStrategy = new CompositeStrategy(
            new ExponentialBackoffStrategy(
                initialDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromMinutes(5),
                maxAttempts: 10),
            new CircuitBreakerStrategy(
                failureThreshold: 5,
                cooldownPeriod: TimeSpan.FromMinutes(1))),
        DegradedStrategy = new RestartStrategy(maxAttempts: 3)
    };
}
```

### Usage Example

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCoven(coven =>
{
    coven.UseDiscordChat(discordConfig);
    coven.UseOpenAIAgents(agentConfig);
    
    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(...);
});

builder.Services.AddDaemonMagistrate(magistrate =>
{
    // Global default: retry transients with backoff
    magistrate.DefaultPolicy = RecoveryPolicy.Default;
    
    // Discord is flaky, be more resilient
    magistrate.Supervise<DiscordChatDaemon>(policy =>
    {
        policy.ExpectedLifetime = DaemonLifetime.Unbounded;
        policy.OnTransient(new ExponentialBackoffStrategy(
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromMinutes(10),
            maxAttempts: 20));
        policy.OnDegraded(new RestartStrategy(maxAttempts: 5));
    });
    
    // Agent daemon is more reliable, use defaults
    magistrate.Supervise<OpenAIAgentDaemon>();
    
    // Escalation: shut down the app if we can't recover
    magistrate.OnEscalation(new ShutdownOnEscalation());
});

var app = builder.Build();
await app.RunAsync();
```

---

## Implementation Notes

### Daemon Cooperation

For the magistrate to work effectively, daemons must cooperate by signaling distress rather than silently retrying or crashing:

```csharp
// Before (silent retry)
private async Task RunAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            await DoWorkAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            // Silent retry - magistrate has no visibility
            await Task.Delay(1000, ct);
        }
    }
}

// After (magistrate-aware)
private async Task RunAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            await DoWorkAsync(ct);
            await Heartbeat(ct); // Signal health
        }
        catch (HttpRequestException ex)
        {
            // Let the magistrate decide
            await SignalDistress(ex, DistressSeverity.Transient, ct);
            
            // Wait for magistrate's recovery signal or continue
            await WaitForRecoveryClearance(ct);
        }
    }
}
```

### Restart Implementation

Restarting a daemon requires coordination:

```csharp
internal class RestartStrategy : IRecoveryStrategy
{
    public async Task<bool> AttemptRecoveryAsync(
        ContractDaemon daemon, 
        CancellationToken ct)
    {
        // 1. Request graceful shutdown
        await daemon.Shutdown(ct);
        
        // 2. Wait for Completed status
        await daemon.WaitFor(Status.Completed, ct);
        
        // 3. Create new instance via DI
        // (Requires daemon factory, not direct instance)
        var newDaemon = _daemonFactory.Create(daemon.GetType());
        
        // 4. Start new instance
        await newDaemon.Start(ct);
        await newDaemon.WaitFor(Status.Running, ct);
        
        // 5. Swap supervision target
        _magistrate.SwapSupervisionTarget(daemon, newDaemon);
        
        return true;
    }
}
```

**Challenge**: This requires daemons to be replaceable at runtime, which may require factory patterns rather than singleton registration.

### Testing

The magistrate should be testable without real daemons:

```csharp
[Fact]
public async Task Magistrate_RetriesTransientFailure_WithBackoff()
{
    // Arrange
    var daemon = new FakeDaemon();
    var policy = new ExponentialBackoffStrategy(
        initialDelay: TimeSpan.FromMilliseconds(10),
        maxDelay: TimeSpan.FromMilliseconds(100),
        maxAttempts: 3);
    
    var magistrate = new DaemonMagistrate(
        new[] { daemon },
        policy,
        _testLogger);
    
    // Act
    await magistrate.StartAsync(CancellationToken.None);
    
    daemon.SimulateDistress(new TimeoutException());
    await Task.Delay(50);
    
    daemon.SimulateDistress(new TimeoutException());
    await Task.Delay(100);
    
    daemon.SimulateRecovery();
    
    // Assert
    Assert.Equal(2, daemon.DistressCount);
    Assert.Equal(1, policy.RecoverySuccessCount);
}
```

---

## Open Questions

1. **Should the magistrate own daemon lifecycle?**  
   Currently, orchestration code starts daemons. Should the magistrate take over starting supervised daemons?

2. **How to handle daemon replacement in DI?**  
   Restarting requires creating new instances. Should daemons be registered as factories?

3. **Heartbeat frequency?**  
   Should daemons emit heartbeats on a schedule, or only during active work?

4. **Multi-daemon coordination?**  
   If daemon A fails and daemon B depends on it, should the magistrate coordinate?

5. **Metrics and observability?**  
   Should the magistrate emit metrics (failure counts, recovery times) for external monitoring?

---

## Alternatives Considered

### 1. Polly Integration

Use [Polly](https://github.com/App-vNext/Polly) for resilience directly in daemons.

**Pros**: Battle-tested, feature-rich, familiar to .NET developers.  
**Cons**: Per-daemon integration, no centralized supervision, doesn't fit Coven's journaling model.

### 2. IHostedService Supervision

Rely on ASP.NET Core's hosted service restart behavior.

**Pros**: Built-in, zero code.  
**Cons**: No backoff, no circuit breaker, no visibility, restarts entire service.

### 3. Actor Model

Use an actor framework (Akka.NET, Orleans) for supervised hierarchies.

**Pros**: Sophisticated supervision strategies.  
**Cons**: Heavyweight, different programming model, overkill for this use case.

---

## Related Work

- **[declarative-covenants.md](declarative-covenants.md)** — Simplifies daemon startup; magistrate would work with covenants.
- **Polly** — .NET resilience library; could be used internally by strategies.
- **Erlang/OTP Supervisors** — Inspiration for supervision trees and recovery strategies.

---

## Summary

The **Daemon Magistrate** fills a gap in Coven's daemon lifecycle management: handling transient failures that occur after successful startup. By introducing:

- **Distress signals** — Daemons communicate "I'm struggling" without terminal failure
- **Recovery strategies** — Configurable backoff, restart, and circuit breaker behaviors
- **Centralized supervision** — One component monitors all supervised daemons
- **Escalation paths** — Clear handoff when recovery fails

...we enable resilient, observable, long-running services that can weather transient infrastructure issues without custom retry logic in every daemon.
