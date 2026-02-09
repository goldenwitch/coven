# Compute Branch

> **Status**: Revised  
> **Created**: 2026-01-25  
> **Revised**: 2026-02-09

---

## Summary

Branch for command execution. Defines efferent entries (`ShellExec`) and afferent entries (`ShellOutput`, `ShellFailure`). Leaves translate these to concrete backends.

Branch package: `Coven.Spellcasting.Compute`  
Companion: `Coven.Agents.Compute` (see [Spellcasting](spellcasting-branch.md))

---

## Entries

Base: `ComputeEntry : Entry`

### Efferent

| Entry | Fields |
|-------|--------|
| `ShellExec` | commandId, command, arguments[], workingDirectory?, environment?, timeout?, streamOutput, useShell |

Structured command + arguments, not shell string. Avoids injection. If shell interpretation needed, set `useShell=true`.

### Afferent

| Entry | Purpose |
|-------|---------|
| `ShellOutput` | Completion (exitCode, stdout, stderr) |
| `ShellOutputChunk` | Streaming fragment (stream, content, timestamp) — implements `IDraft` |
| `ShellFailure` | Execution failure (failureKind, message) |

`ShellOutputChunk` uses `IDraft` marker—windowed into final `ShellOutput` via `StreamWindowingDaemon` pattern.

All carry `CommandId` for correlation.

---

## Leaves

Each leaf extends `ContractDaemon`, tails `IScrivener<ComputeEntry>`, processes efferent entries, writes afferent results:

```
DAEMON PosixShellDaemon
  tails: IScrivener<ComputeEntry>
  
  ON ShellExec { command-id, command, arguments, working-dir }:
    result = execute command in shell
    WRITE ShellOutput { command-id, stdout, stderr, exit-code }
    
  ON error:
    WRITE ShellFailure { command-id, error }
```

| Leaf | Backend | Package |
|------|---------|--------|
| `PosixShellDaemon` | `Process.Start` (POSIX) | `Coven.Spellcasting.Compute.Posix` |
| `WindowsShellDaemon` | `Process.Start` (Windows) | `Coven.Spellcasting.Compute.Windows` |
| `MockShellDaemon` | Scripted responses (testing) | `Coven.Spellcasting.Compute.Mock` |

Leaves can filter by command allowlist or working directory scope.

---

## Checklist

- [ ] `ComputeEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `PosixShellDaemon` extends `ContractDaemon`
- [ ] `MockShellDaemon` for testing
- [ ] Streaming via `ShellOutputChunk` + windowing
- [ ] `Coven.Agents.Compute` companion with tool definitions and transmuters
