# Compute Sub-branch

> **Status**: Draft  
> **Created**: 2026-01-25  
> **Parent**: [Spellcasting Branch](spellcasting-branch.md)

---

## Summary

Sub-branch of Spellcasting for command execution. Spells write `ShellExec` (efferent intent). Leaf daemons tail via `TailAsync`, execute against their backend, write `ShellOutput` or `ShellFault` (afferent fulfillment).

---

## Entries

Base: `ComputeEntry : Entry`

### Efferent (Intent)

| Entry | Fields |
|-------|--------|
| `ShellExec` | commandId, command, arguments[], workingDirectory?, environment?, timeout?, streamOutput, useShell |

Structured command + arguments, not shell string. Avoids injection. If shell interpretation needed, set `useShell=true`.

### Afferent (Fulfillment)

| Entry | Purpose |
|-------|---------|
| `ShellOutput` | Completion (exitCode, stdout, stderr) |
| `ShellOutputChunk` | Streaming fragment (stream, content, timestamp) — implements `IDraft` |
| `ShellFault` | Execution failure (faultKind, message) |

`ShellOutputChunk` uses `IDraft` marker—windowed into final `ShellOutput` via `StreamWindowingDaemon` pattern.

All carry `CommandId` for correlation.

---

## Leaves

Each leaf extends `ContractDaemon`, tails `IScrivener<ComputeEntry>`, processes intent entries, writes fulfillment:

```
DAEMON LocalShellDaemon
  tails: IScrivener<ComputeEntry>
  
  ON ShellExec { command-id, command, arguments, working-dir }:
    result = execute command in shell
    WRITE ShellOutput { command-id, stdout, stderr, exit-code }
    
  ON error:
    WRITE ShellFault { command-id, error }
```

| Leaf | Backend |
|------|--------|
| `LocalShellDaemon` | `Process.Start` |
| `MockShellDaemon` | Scripted responses (testing) |

Leaves can filter by command allowlist or working directory scope.

---

## Checklist

- [ ] `ComputeEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `LocalShellDaemon` extends `ContractDaemon`
- [ ] `MockShellDaemon` for testing
- [ ] Streaming via `ShellOutputChunk` + windowing
