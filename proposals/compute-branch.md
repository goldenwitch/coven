# Compute Branch

> **Status**: Draft  
> **Created**: 2026-01-25  
> **Depends on**: Spellcasting and Target Branches (tooling-branch.md)

---

## Problem

Coven's spellcasting architecture needs to execute shell commands. Agents reason about tasks, but accomplishing real work requires interaction with the operating system—running builds, executing scripts, querying system state, transforming files.

### The Gap

```
                    Spellcasting Branch
                           │
             ┌─────────────┴─────────────┐
             │                           │
      FileSystem Branch           Compute Branch
      (read/write files)          (execute commands)
             │                           │
       ┌─────┴─────┐               ┌─────┴─────┐
       │           │               │           │
    PosixFS    WindowsFS      PosixShell  WindowsShell
```

The FileSystem branch (separate proposal) handles file I/O. But many operations require **execution**:

| Task | File I/O Alone | Needs Compute |
|------|----------------|---------------|
| Read config file | ✓ | |
| Build project | | ✓ `npm run build` |
| Run tests | | ✓ `pytest` |
| Query git status | | ✓ `git status` |
| Format code | | ✓ `prettier --write` |
| Install dependencies | | ✓ `pip install -r` |

Without a Compute branch, agents cannot act on the world—they can only read and write passive data.

### Why a Branch?

Shell execution could be implemented as a utility. Why model it as a full branch with journals?

1. **Auditability**: Every command and its output is journaled. Replay, debug, compliance.
2. **Streaming**: Long-running commands emit incremental output. Windowing policies decide when to surface results.
3. **Swappability**: PosixShell for Linux/macOS, WindowsShell for Windows, future RemoteShell for sandboxed execution—same interface.
4. **Consistency**: Follows established Coven patterns. Agents interact with Compute the same way they interact with Chat or FileSystem.

---

## Entry Types

Following the efferent/afferent pattern:

### Efferent (Spine → Leaf)

| Entry | Purpose |
|-------|---------|
| `ShellExec` | Request to execute a command |

```
ShellExec
├── CommandId       : string      # Correlation ID for matching responses
├── Command         : string      # Executable name or path
├── Arguments       : string[]    # Argument array (NOT shell-interpreted)
├── WorkingDirectory: string?     # Optional; defaults to process cwd
├── Environment     : Dictionary? # Additional/override env vars
├── Timeout         : TimeSpan?   # Optional execution timeout
├── StreamOutput    : bool        # Whether to emit chunks or wait for completion
├── UseShell        : bool        # If true, invoke via shell interpreter (bash -c / cmd /c)
```

**Design decision**: Structured command + arguments, not a shell string.

- Avoids injection vulnerabilities (`; rm -rf /`)
- Explicit about what's executed vs. interpreted
- Arguments with spaces handled correctly without escaping
- If shell interpretation is needed, explicitly invoke `bash -c` or `cmd /c`

### Afferent (Leaf → Spine)

| Entry | Purpose |
|-------|---------|
| `ShellOutputChunk` | Streaming output fragment |
| `ShellOutput` | Command completed successfully |
| `ShellFault` | Command failed to execute |

```
ShellOutputChunk
├── CommandId   : string        # Correlates to ShellExec
├── Stream      : enum          # Stdout | Stderr
├── Content     : string        # Chunk content
├── Timestamp   : DateTimeOffset

ShellOutput
├── CommandId   : string
├── ExitCode    : int
├── Stdout      : string        # Complete stdout (if not streamed)
├── Stderr      : string        # Complete stderr (if not streamed)
├── Duration    : TimeSpan
├── Completed   : DateTimeOffset

ShellFault
├── CommandId   : string
├── FaultKind   : enum          # NotFound | PermissionDenied | Timeout | Cancelled | Unknown
├── Message     : string
├── Exception   : string?       # Serialized exception for debugging
```

### Exit Code Semantics

Exit codes are **not** automatically faults. Many commands use non-zero exits meaningfully:

| Command | Exit 0 | Exit 1 | Exit 2+ |
|---------|--------|--------|---------|
| `grep` | Found matches | No matches | Error |
| `diff` | Files identical | Files differ | Error |
| `test -f` | File exists | File missing | Syntax error |

**Policy**: The leaf emits `ShellOutput` for any exit. The spine decides whether the exit code constitutes success. `ShellFault` is reserved for failures to execute at all (command not found, permission denied, timeout).

---

## Streaming Model

### Chunk Granularity

Stdout and stderr are byte streams. The leaf must buffer into string chunks. Options:

| Strategy | Behavior | Trade-offs |
|----------|----------|------------|
| Line-buffered | Emit on newline | Natural for logs; may delay partial lines |
| Size-buffered | Emit at N bytes | Predictable chunks; may split mid-line |
| Time-buffered | Emit after N ms | Responsive; many tiny chunks |
| Hybrid | Line OR timeout | Best of both; more complex |

**Recommendation**: Hybrid line + timeout (100ms). Most output is line-oriented. Timeout ensures responsiveness for commands that write without newlines (progress indicators, prompts).

### Interleaving

Stdout and stderr are separate streams. Options:

1. **Separate entries**: `ShellOutputChunk` with `Stream` field distinguishes them
2. **Merged stream**: Single stream, lose distinction
3. **Ordered tuple**: `(stdout_chunk, stderr_chunk)` per emission

**Recommendation**: Option 1 (separate entries with stream field). Preserves distinction. Consumers can merge or filter as needed.

### Windowing Policy

Chunks accumulate until a windowing policy emits. For shell output:

```
┌─────────────────────────────────────────────────────┐
│                    Chunk Buffer                      │
│  [chunk1] [chunk2] [chunk3] [chunk4] [chunk5] ...   │
└─────────────────────────────────────────────────────┘
                         │
                    Window Policy
                         │
              ┌──────────┴──────────┐
              │                     │
        FinalOnly              ParagraphBoundary
    (wait for ShellOutput)     (emit on blank line)
              │                     │
              │                 LineCount(N)
              │              (emit every N lines)
              │                     │
              │               Composite(OR)
              │                     │
              └──────────┬──────────┘
                         │
                    Emit Decision
```

**Built-in policies for Compute**:

| Policy | Behavior | Use Case |
|--------|----------|----------|
| `FinalOnly` | Wait for `ShellOutput` | Batch scripts, short commands |
| `LineCount(n)` | Emit every n lines | Build logs, test output |
| `Timeout(ms)` | Emit after silence | Interactive responsiveness |
| `Composite` | OR multiple policies | Line count OR timeout |

**Completion marker**: `ShellOutput` or `ShellFault` flushes remaining chunks.

---

## Platform-Specific Shell Leaves

POSIX and Windows shells differ fundamentally:

| Concern | POSIX (bash/sh) | Windows (cmd/PowerShell) |
|---------|-----------------|-------------------------|
| Command syntax | `ls -la` | `dir /a` or `Get-ChildItem` |
| Env var expansion | `$VAR` or `${VAR}` | `%VAR%` or `$env:VAR` |
| Path separator | `:` (PATH) | `;` (PATH) |
| Executable resolution | `$PATH` lookup | `%PATH%` + `PATHEXT` |
| Quoting | Single/double distinct | Double only (cmd) |
| Exit codes | 0-255 | 0-65535 |
| Signal handling | SIGTERM, SIGKILL | TerminateProcess |
| Process groups | Native | Job objects |
| Shell built-ins | `cd`, `export`, `source` | `cd`, `set`, `call` |

A single "LocalShell" would paper over these differences. Platform-specific leaves handle them explicitly.

---

## Leaf: PosixShell

Command execution for POSIX-compliant systems (Linux, macOS, BSD).

### Architecture

```
                ┌─────────────────────────────────────────┐
                │            PosixShellDaemon             │
                │                                         │
  ShellExec ──► │  ┌─────────┐    ┌──────────────────┐   │
  (efferent)    │  │ Command │    │  fork/exec       │   │
                │  │  Queue  │───►│  (per command)   │   │
                │  └─────────┘    └────────┬─────────┘   │
                │                          │             │
                │              ┌───────────┼───────────┐ │
                │              │           │           │ │
                │              ▼           ▼           ▼ │
                │           stdout      stderr      exit │
                │              │           │           │ │
                │              └───────────┼───────────┘ │
                │                          │             │
                │                          ▼             │
                │  ┌───────────────────────────────────┐ │
                │  │  Chunk Emitter (line + timeout)   │ │
                │  └───────────────────────────────────┘ │
                │                          │             │
                └──────────────────────────┼─────────────┘
                                           │
                                           ▼
                              ShellOutputChunk / ShellOutput
                                      (afferent)
```

### Configuration

| Setting | Type | Default | Purpose |
|---------|------|---------|--------|
| `DefaultShell` | `string` | `/bin/sh` | Shell for `UseShell` mode |
| `InheritEnvironment` | `bool` | `true` | Pass parent env to child |
| `ConcurrencyLimit` | `int` | `4` | Max parallel commands |
| `DefaultTimeout` | `TimeSpan?` | None | Default if not specified |
| `GracePeriod` | `TimeSpan` | `5s` | SIGTERM→SIGKILL delay |

### Signal Handling

| Action | Signal Sequence |
|--------|----------------|
| Timeout | SIGTERM → wait grace period → SIGKILL |
| Cancellation | SIGTERM → wait grace period → SIGKILL |
| Immediate kill | SIGKILL |

### Shell Interpretation

When `ShellExec.UseShell = true`:

```
Command:   "echo $HOME && ls | grep foo"

Translated: /bin/sh -c "echo $HOME && ls | grep foo"
```

This enables pipes, redirects, globs, and variable expansion — with injection risks the caller accepts.

---

## Leaf: WindowsShell

Command execution for Windows systems.

### Architecture

```
                ┌─────────────────────────────────────────┐
                │           WindowsShellDaemon            │
                │                                         │
  ShellExec ──► │  ┌─────────┐    ┌──────────────────┐   │
  (efferent)    │  │ Command │    │  CreateProcess   │   │
                │  │  Queue  │───►│  (per command)   │   │
                │  └─────────┘    └────────┬─────────┘   │
                │                          │             │
                │              ┌───────────┼───────────┐ │
                │              │           │           │ │
                │              ▼           ▼           ▼ │
                │           stdout      stderr      exit │
                │              │           │           │ │
                │              └───────────┼───────────┘ │
                │                          │             │
                │                          ▼             │
                │  ┌───────────────────────────────────┐ │
                │  │  Chunk Emitter (line + timeout)   │ │
                │  └───────────────────────────────────┘ │
                │                          │             │
                └──────────────────────────┼─────────────┘
                                           │
                                           ▼
                              ShellOutputChunk / ShellOutput
                                      (afferent)
```

### Configuration

| Setting | Type | Default | Purpose |
|---------|------|---------|--------|
| `DefaultShell` | `string` | `cmd.exe` | Shell for `UseShell` mode |
| `PreferPowerShell` | `bool` | `false` | Use `pwsh`/`powershell` instead |
| `InheritEnvironment` | `bool` | `true` | Pass parent env to child |
| `ConcurrencyLimit` | `int` | `4` | Max parallel commands |
| `DefaultTimeout` | `TimeSpan?` | None | Default if not specified |
| `UseJobObjects` | `bool` | `true` | Track child processes |

### Process Termination

Windows lacks POSIX signals. Termination is abrupt:

| Action | Mechanism |
|--------|----------|
| Timeout | TerminateProcess (immediate) |
| Cancellation | TerminateProcess (immediate) |

**Job Objects**: When `UseJobObjects = true`, child processes spawned by the command are also terminated. Without this, orphan processes can survive.

### Shell Interpretation

When `ShellExec.UseShell = true`:

```
With cmd.exe:
  Command:   "echo %USERNAME% && dir | findstr foo"
  Translated: cmd.exe /c "echo %USERNAME% && dir | findstr foo"

With PowerShell:
  Command:   "Write-Host $env:USERNAME; Get-ChildItem | Where Name -match foo"
  Translated: pwsh -NoProfile -Command "Write-Host $env:USERNAME; ..."
```

### Executable Resolution

Windows uses `PATHEXT` to find executables:

```
Command: "python"
Search:  python.exe, python.cmd, python.bat, python.ps1 (in %PATH%)
```

The leaf resolves the full path before execution for auditability.

### Daemon Lifecycle

- **Start**: Begin tailing the efferent journal for `ShellExec` entries
- **Running**: Spawn processes, manage output streams, emit chunks
- **Shutdown**: Cancel pending commands, await running processes (with timeout), complete

### Concurrency

Multiple commands can execute concurrently. Each `ShellExec` spawns an independent process. The `CommandId` correlates requests to responses.

**Consideration**: Should there be a concurrency limit? Unbounded parallelism could exhaust system resources.

**Recommendation**: Configurable limit (default: 4 concurrent commands). Queue excess requests. Emit `ShellFault(Throttled)` if queue is full.

### Timeout Handling

If `ShellExec.Timeout` is set:
1. Start a timer when the process spawns
2. On timeout: kill process, emit `ShellFault(Timeout)`
3. Include partial output captured before timeout

### Cancellation

The daemon respects the application cancellation token. On cancellation:
1. Stop accepting new `ShellExec` entries
2. Kill running processes (graceful SIGTERM, then SIGKILL after grace period)
3. Emit `ShellFault(Cancelled)` for interrupted commands
4. Transition to Completed

---

## Alternatives Considered

### Shell String Instead of Structured Command

```
ShellExec { CommandLine: "git commit -m 'fixed bug'" }
```

**Rejected because**:
- Injection risk: untrusted input could escape
- Platform variance: different shells parse differently
- Escaping complexity: quotes, spaces, special chars
- Less explicit: harder to audit what actually runs

The structured form is more verbose but safer and portable.

### Single Output Entry Instead of Chunks

```
ShellOutput { Stdout: "...", Stderr: "..." }  // All at once
```

**Rejected because**:
- Long-running commands block until completion
- No incremental feedback for builds, tests, deployments
- Inconsistent with streaming patterns in Chat/Agents branches

Streaming is essential for good UX on long commands.

### Merged Stdout/Stderr Stream

**Rejected because**:
- Loses ability to style/filter stderr differently
- Some consumers need to distinguish errors from output
- Easy to merge downstream if needed; impossible to split after merge

### Interactive Session Support

**Deferred**. Interactive shells (expect-style) add significant complexity:
- PTY allocation
- Escape sequence handling
- Session state management
- Input injection timing

Current design supports batch execution only. Interactive sessions could be a future extension with a separate entry type (`ShellSession`, `ShellInput`).

---

## Checklist

### Entry Types
- [ ] Define `ShellExec` efferent entry
- [ ] Define `ShellOutput` afferent entry
- [ ] Define `ShellOutputChunk` afferent entry
- [ ] Define `ShellFault` afferent entry with fault kinds
- [ ] Add entries to Compute branch journal schema

### PosixShell Leaf
- [ ] Implement `PosixShellDaemon` with lifecycle
- [ ] Process spawning via fork/exec
- [ ] Stdout/stderr streaming with hybrid line+timeout buffering
- [ ] Exit code capture and `ShellOutput` emission
- [ ] Signal-based timeout (SIGTERM → SIGKILL)
- [ ] Cancellation support with graceful shutdown
- [ ] Configurable concurrency limit

### WindowsShell Leaf
- [ ] Implement `WindowsShellDaemon` with lifecycle
- [ ] Process spawning via CreateProcess
- [ ] Stdout/stderr streaming with hybrid line+timeout buffering
- [ ] Exit code capture and `ShellOutput` emission
- [ ] Job object management for child process tracking
- [ ] TerminateProcess for timeout/cancellation
- [ ] PATHEXT-aware executable resolution
- [ ] PowerShell vs cmd.exe mode selection

### Windowing
- [ ] `FinalOnly` policy (default)
- [ ] `LineCount(n)` policy
- [ ] `Timeout(ms)` policy
- [ ] `Composite` policy for OR composition

### Integration
- [ ] DI registration extensions
- [ ] Builder pattern for leaf configuration
- [ ] E2E tests with temp directory execution
- [ ] Documentation in architecture/

---

## Open Questions

1. **Working directory default**: Should it be process cwd, a configured base path, or required on every `ShellExec`?

2. **Environment inheritance**: Should child processes inherit the full parent environment, or start clean with explicit vars only?

3. **Output encoding**: Assume UTF-8? Detect from locale? Configurable per-command?

4. **Resource limits**: Should LocalShell support memory/CPU limits on spawned processes? Platform-specific complexity.

5. **Shell-required commands**: Pipes, redirects, globbing require a shell. Explicit `ShellExec.UseShell: true` flag, or always require explicit `bash -c`?
