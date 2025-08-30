
# Coven.Spellcasting.Agents.Validation - Design

> **Purpose.** Provide a tiny, idempotent **AgentValidation** extension point that can run as a regular **MagikBlock** (traditional compute) or be called manually anywhere in the pipeline. This leaves **MagikUser** reserved for *agent runtime*, while validation handles *environment readiness* (e.g., installing CLIs, checking versions) with a minimal public surface.

---

## Goals & Principles

- **MagikBlock, not MagikUser.** Validation is configuration/compute; it composes as a normal block.
- **Idempotent by design.** Safe to run multiple times (spec‑hashed stamp files + re‑probe).
- **Permission‑aware.** Respects `SpellContext.Permissions` (skips provisioning if `RunCommand` is not granted).
- **Small public API.** Only expose what app/agent authors must implement or call.
- **Moveable.** Works before building, inside a ritual, or after `builder.Done()` via a manual call—no behavior change.

> **Depends on existing types** from `Coven.Spellcasting.Agents`: `SpellContext` (sealed, facet‑capable), `AgentPermissions`, and the `RunCommand` action symbol. Validation does not add new facets or change these types.

---

## Public API

**Namespace:** `Coven.Spellcasting.Agents.Validation`

```csharp
namespace Coven.Spellcasting.Agents.Validation;

/// <summary>
/// Idempotent readiness for a single agent (e.g., install CLI, check versions).
/// Implementations MUST be safe to call repeatedly.
/// </summary>
public interface IAgentValidation
{
    /// Stable identifier, e.g., "codex".
    string AgentId { get; }

    /// Fast probe: are requirements already met?
    ValueTask<bool> IsReadyAsync(SpellContext context, CancellationToken ct = default);

    /// Bring the system into a ready state (idempotent).
    Task ProvisionAsync(SpellContext context, CancellationToken ct = default);

    /// Deterministic token representing the required spec (OS, versions, flags, etc.).
    /// Change this when your requirements change to invalidate the prior stamp.
    ValueTask<string> GetSpecAsync(SpellContext context, CancellationToken ct = default);
}

/// <summary>Outcome per validator after EnsureAsync completes.</summary>
public sealed record AgentValidationOutcome(
    string AgentId,
    bool Satisfied,   // final state
    bool Changed      // did we modify the system?
);

public static class AgentValidationRunner
{
    /// <summary>
    /// Ensure all validators are satisfied. Idempotent via per-agent spec stamps.
    /// If context.Permissions does not allow RunCommand, only probes are run.
    /// cacheRoot: null ⇒ ~/.coven/spellwork/provision
    /// </summary>
    public static Task<IReadOnlyList<AgentValidationOutcome>> EnsureAsync(
        IEnumerable<IAgentValidation> validators,
        SpellContext context,
        string? cacheRoot = null,
        bool refresh = false,
        CancellationToken ct = default);
}

/// <summary>
/// Convenience MagikBlock so validation can run as a ritual step.
/// Identity block: SpellContext → SpellContext.
/// Probably needs an implementation of DoMagik that calls our validation.
/// </summary>
public sealed class ValidateAgentsBlock : IMagikBlock<SpellContext, SpellContext>
{
    public ValidateAgentsBlock(IEnumerable<IAgentValidation> validators,
                               string? cacheRoot = null,
                               bool refresh = false);

    public Task<SpellContext> DoMagik(SpellContext input);
}
```
**Publicness rationale**
- `IAgentValidation` is implemented by app/agent authors.
- `AgentValidationRunner` is called manually anywhere (including after `Done()`).
- `AgentValidationOutcome` is returned to callers for logging/telemetry.
- `ValidateAgentsBlock` is a **sealed** convenience to drop validation into a ritual. No additional public helpers.

---

## Behavior & Algorithm (EnsureAsync)

1. **Compute stamp path** per validator:
   - `hash = SHA‑256(GetSpecAsync(context))`
   - `stamp = ~/.coven/spellwork/provision/<AgentId>/<hash>.ok` (or under `cacheRoot` if provided)
2. **Fast path**: if `stamp` exists → run `IsReadyAsync` once; if true → return `{ Satisfied: true, Changed: false }`.
3. **Probe** current state via `IsReadyAsync`.
   - If ready → write/refresh `stamp`, return `{ true, false }`.
   - If not ready and `RunCommand` is **not** allowed → return `{ false, false }`.
4. **Lock & repair**:
   - Acquire exclusive file lock `<dir>/.lock` (prevents concurrent provisioners).
   - Re‑probe; if ready, stamp + `{ true, false }`.
   - Call `ProvisionAsync`.
   - Re‑probe; if not ready, throw `InvalidOperationException`.
   - Stamp + `{ true, true }`.
5. **Return** outcomes for all validators in order.

**Idempotency**: repeated calls are O(1) after the first success unless `refresh` is set or the spec changes.

**Concurrency**: lock guards against duplicate installers across processes.

**Permissions**: provisioning steps only run when `RunCommand` is granted in `SpellContext.Permissions`.

---

## Typical Usage Patterns

### A) As the first block in a startup ritual
```csharp
var validators = new IAgentValidation[] { new CodexValidation() };

var startup = new MagikBuilder<SpellContext, SpellContext>()
    .MagikBlock(new ValidateAgentsBlock(validators))
    .Done();

spellContext = await startup.Ritual<SpellContext, SpellContext>(spellContext);
```

### B) Manual call before your main ritual
```csharp
var ctx = new SpellContext { Permissions = AgentPermissions.FullAuto() };

var outcomes = await AgentValidationRunner.EnsureAsync(
    new IAgentValidation[] { new CodexValidation() },
    ctx);

// build & run your actual ritual…
```

### C) Manual call after `builder.Done()` (no block)
```csharp
var ritual = new MagikBuilder<..., ...>()
    .MagikBlock(...)
    .Done();

await AgentValidationRunner.EnsureAsync(validators, spellContext);
var result = await ritual.Ritual<..., ...>(input);
```

All three placements are equivalent and safe to reorder.

---

## Example: A Simple Validator

```csharp
using Coven.Spellcasting.Agents;
using Coven.Spellcasting.Agents.Validation;

public sealed class CodexValidation : IAgentValidation
{
    public string AgentId => "codex";

    public ValueTask<string> GetSpecAsync(SpellContext ctx, CancellationToken ct = default)
    {
        var os = Environment.OSVersion.Platform.ToString();
        const string node = ">=18"; const string npm = ">=9"; const string codex = ">=0.5";
        return ValueTask.FromResult($"os={os};node{node};npm{npm};codex{codex}");
    }

    public async ValueTask<bool> IsReadyAsync(SpellContext ctx, CancellationToken ct = default)
        => await Has("codex", "--version", ct)
        && await Has("node", "-v", ct)
        && await Has("npm", "-v", ct);

    public async Task ProvisionAsync(SpellContext ctx, CancellationToken ct = default)
    {
        await Run("npm", new[] { "install", "-g", "@openai/codex" }, ct);
    }

    // Private helpers
    static async Task<bool> Has(string file, string arg, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo { FileName = file, UseShellExecute = false };
        psi.ArgumentList.Add(arg);
        try { using var p = System.Diagnostics.Process.Start(psi)!; await p.WaitForExitAsync(ct); return p.ExitCode == 0; }
        catch { return false; }
    }

    static async Task Run(string file, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo { FileName = file, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!; await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException($"{file} failed with {p.ExitCode}");
    }
}
```

---

## Non‑Public Implementation Notes

- The default `cacheRoot` is `~/.coven/spellwork/provision` (create if missing).
- Stamp files contain a UTC timestamp for diagnostics; only existence matters.
- File lock uses `FileShare.None` on `OpenOrCreate` with a `.lock` file in the agent stamp directory.
- `ValidateAgentsBlock` is a tiny wrapper over `AgentValidationRunner.EnsureAsync` and returns its input unchanged (identity).

---

## Public Surface Summary

| Type | Kind | Public? | Reason |
|---|---|---|---|
| `IAgentValidation` | interface | **Yes** | Implemented by app/agent authors |
| `AgentValidationOutcome` | record | **Yes** | Returned to callers; minimal shape |
| `AgentValidationRunner` | static class | **Yes** | Called manually anywhere (pre/post `Done`) |
| `ValidateAgentsBlock` | MagikBlock | **Yes (sealed)** | Convenience to run validation in rituals |
| Stamp/lock/process helpers | — | **No** | Internal details; free to evolve |

---
