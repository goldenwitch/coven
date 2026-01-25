# Proposal: Non-Null Constraints on ITransmuter Output Types

## Status

**Implemented**: Added `where TOut : notnull` constraint using the **inline type check approach** (not the marker interface). See [Implementation Notes](#implementation-notes) for rationale.

## Problem Statement

The current `ITransmuter<TIn, TOut>` interface permits nullable output types, which introduces several issues:

### 1. Semantic Ambiguity

When `TOut` is nullable (e.g., `ITransmuter<OpenAIEntry, ResponseItem?>`), null can mean:
- "Filter this entry out" (intentional skip)
- "An error occurred" (failure case)
- "No mapping exists" (unmapped variant)

The current codebase uses null exclusively as a **filter mechanism** — signaling "do not include this entry in the output collection." However, this semantic is implicit and not enforced by the type system.

### 2. Caller Burden

Every caller of a nullable-output transmuter must handle null explicitly:

```csharp
// From DefaultOpenAITranscriptBuilder.cs
ResponseItem? item = await _entryToItem.Transmute(entry, cancellationToken).ConfigureAwait(false);
if (item is not null)
{
    buffer.Add(item);
}
```

This null-check pattern is repeated wherever the transmuter is called, violating DRY and creating opportunities for bugs if a caller forgets the check.

### 3. Violation of Transmuter Principles

The [Coven.Transmutation README](../src/Coven.Transmutation/README.md) states transmuters should be:
- **Pure**: no observable side-effects
- **Deterministic**: same inputs → same outputs

A transmuter that returns null for "filter this out" is arguably pure, but it **conflates two fundamentally different concerns**:
1. **Mapping**: transforming one type to another (total function)
2. **Filtering**: deciding which entries to include (predicate)

This conflation violates the Single Responsibility Principle. A transmuter should be a **pure transform** — given input A, produce output B. It should not encode branching logic that decides whether to produce output at all.

## Current Nullable Implementations

### Implementation 1: OpenAIEntryToResponseItemTransmuter

**File**: [OpenAIEntryToResponseItemTransmuter.cs](../src/Coven.Agents.OpenAI/OpenAIEntryToResponseItemTransmuter.cs)

**Interface**: `ITransmuter<OpenAIEntry, ResponseItem?>`

**What it filters out**: 
- `OpenAIAfferentChunk` (streaming response chunks)
- `OpenAIAfferentThoughtChunk` (streaming thought chunks)
- `OpenAIThought` (complete thought entries)
- `OpenAIAck` (acknowledgement entries)
- `OpenAIEfferentThoughtChunk` (outgoing thought chunks)
- `OpenAIStreamCompleted` (stream completion markers)

**What it keeps**:
- `OpenAIEfferent` (user messages) → `ResponseItem.CreateUserMessageItem`
- `OpenAIAfferent` (assistant messages) → `ResponseItem.CreateAssistantMessageItem`

**Business reason**: Only user and assistant *text messages* participate in prompt construction for the OpenAI Responses API. Chunks, thoughts, acks, and stream markers are internal bookkeeping that shouldn't be sent back to the model.

**Called from**: [DefaultOpenAITranscriptBuilder.cs](../src/Coven.Agents.OpenAI/DefaultOpenAITranscriptBuilder.cs) via `ITransmuter<OpenAIEntry, ResponseItem?>` DI registration.

### Implementation 2: DiscordOpenAITemplatingTransmuter

**File**: [DiscordOpenAITemplatingTransmuter.cs](../src/samples/01.DiscordAgent/DiscordOpenAITemplatingTransmuter.cs)

**Interface**: `ITransmuter<OpenAIEntry, ResponseItem?>`

**What it filters**: Same as above (thoughts/acks/chunks → null)

**What it keeps**: Same as above, but with Discord-specific templating:
- User messages decorated with `[discord username:{sender}]`
- Assistant messages decorated with `[assistant:{model}]`

**Business reason**: Demonstrates how apps can customize prompt construction while still filtering out non-message entries.

**Called from**: Same as above (replaces default transmuter via DI).

### Usage Sites

| File | How Null Is Handled |
|------|---------------------|
| [DefaultOpenAITranscriptBuilder.cs](../src/Coven.Agents.OpenAI/DefaultOpenAITranscriptBuilder.cs#L23-L26) | `if (item is not null) buffer.Add(item)` |
| [DefaultOpenAITranscriptBuilder.cs](../src/Coven.Agents.OpenAI/DefaultOpenAITranscriptBuilder.cs#L37-L40) | `if (newestItem is not null) buffer.Add(newestItem)` |

### Non-Nullable Implementations (Reference)

All other transmuter implementations in the codebase return non-null values:

- `OpenAIResponseOptionsTransmuter`: `ITransmuter<OpenAIClientConfig, ResponseCreationOptions>`
- `OpenAITransmuter`: `IImbuingTransmuter<OpenAIEntry, long, AgentEntry>` — throws `ArgumentOutOfRangeException` for unknown types
- `DiscordTransmuter`: `IImbuingTransmuter<DiscordEntry, long, ChatEntry>` — throws for unknown types
- `ConsoleTransmuter`: `IImbuingTransmuter<ConsoleEntry, long, ChatEntry>` — throws for unknown types
- All `IBatchTransmuter` implementations return valid `BatchTransmuteResult<TChunk, TOutput>`

## Analysis

### The Core Problem: Filtering at the Wrong Layer

Both nullable transmuters are filtering **at transmutation time** when the filtering should happen **before** the transmuter is invoked. The transmuter receives all `OpenAIEntry` variants, then decides which ones to process. This is backwards.

The question is not "how should the transmuter signal filtering?" but rather "why is the transmuter receiving entries it shouldn't transform?"

### Existing Coven Constructs for Filtering

Coven already has patterns that could handle filtering:

1. **Type-based filtering via `OfType<T>()` / pattern matching on async enumerables**
   - LINQ's `OfType<T>()` or `Where(e => e is T)` can filter before transmutation
   - The journal `ReadBackwardAsync()` returns `IAsyncEnumerable` which supports LINQ operators

2. **`IShatterPolicy<TEntry>`** — Returns zero or more entries
   - A shatter policy that returns empty sequence for unwanted types effectively filters
   - However, shatter operates on entries of the same type (TEntry → TEntry), not cross-type

3. **`IScrivener<T>.WaitForAsync(predicate)`** — Built-in predicate filtering
   - Already supports predicate-based filtering for waiting
   - Not directly applicable to batch reads, but shows the pattern exists

4. **`IWindowPolicy<TChunk>.ShouldEmit()`** — Decision predicate
   - Controls emission timing, not filtering, but demonstrates predicate patterns

### Why This Pattern Exists

The filtering transmuter pattern emerged because:

1. `OpenAIEntry` is a discriminated union (8+ derived types)
2. Only 2 of those types (`OpenAIEfferent`, `OpenAIAfferent`) participate in prompts
3. The transmuter was given responsibility for both "which types?" and "how to transform?"

This was expedient but conflates concerns.

### Why Other Transmuters Don't Use Null

Non-nullable transmuters handle the "what about unmapped types?" question differently:
- **Throw**: `ArgumentOutOfRangeException` for genuinely invalid inputs (see `OpenAITransmuter`, `DiscordTransmuter`)
- **Total function**: Accept only the types they can transform (via narrower input types)

The throwing pattern is cleaner because the transmuter either succeeds (returns a value) or fails (throws). The caller knows something went wrong rather than silently dropping data.

## Proposed Change

### Recommendation: Add `where TOut : notnull` Constraint

```csharp
public interface ITransmuter<TIn, TOut>
    where TOut : notnull
{
    Task<TOut> Transmute(TIn Input, CancellationToken cancellationToken = default);
}
```

This change enforces that transmuters are **pure transforms** — they accept input and produce output. No branching logic, no filtering. The constraint makes this expectation explicit in the type system.

## Migration Plan

### Approach: Filter Before Transmutation

The cleanest solution uses existing LINQ operators to filter the async enumerable before transmutation. No new constructs are needed.

### Migration for OpenAIEntryToResponseItemTransmuter

**Step 1**: Narrow the input type to a union interface

Create an interface for the types that participate in prompts:

```csharp
// New marker interface for prompt-participating entries
internal interface IOpenAIPromptEntry { }

public sealed record OpenAIEfferent(...) : OpenAIEntry(Sender), IOpenAIPromptEntry;
public sealed record OpenAIAfferent(...) : OpenAIEntry(Sender), IOpenAIPromptEntry;
```

**Step 2**: Change transmuter signature to non-nullable

```csharp
// Before
internal sealed class OpenAIEntryToResponseItemTransmuter : ITransmuter<OpenAIEntry, ResponseItem?>

// After
internal sealed class OpenAIEntryToResponseItemTransmuter : ITransmuter<IOpenAIPromptEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(IOpenAIPromptEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIEfferent u => Task.FromResult(ResponseItem.CreateUserMessageItem(u.Text)),
            OpenAIAfferent a => Task.FromResult(ResponseItem.CreateAssistantMessageItem(a.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input), $"Unexpected prompt entry type: {Input.GetType().Name}")
        };
    }
}
```

**Step 3**: Update DefaultOpenAITranscriptBuilder to filter before transmuting

```csharp
// Before
await foreach ((_, OpenAIEntry entry) in _journal.ReadBackwardAsync(...))
{
    ResponseItem? item = await _entryToItem.Transmute(entry, cancellationToken);
    if (item is not null) buffer.Add(item);
    ...
}

// After
await foreach ((_, OpenAIEntry entry) in _journal.ReadBackwardAsync(...))
{
    if (entry is IOpenAIPromptEntry promptEntry)
    {
        ResponseItem item = await _entryToItem.Transmute(promptEntry, cancellationToken);
        buffer.Add(item);
    }
    ...
}
```

Alternatively, using LINQ's `OfType<T>()` if available on the async enumerable:

```csharp
await foreach ((_, IOpenAIPromptEntry promptEntry) in _journal.ReadBackwardAsync(...)
    .Where(x => x.entry is IOpenAIPromptEntry)
    .Select(x => (x.journalPosition, (IOpenAIPromptEntry)x.entry)))
{
    ResponseItem item = await _entryToItem.Transmute(promptEntry, cancellationToken);
    buffer.Add(item);
    ...
}
```

### Migration for DiscordOpenAITemplatingTransmuter

Same approach — change to `ITransmuter<IOpenAIPromptEntry, ResponseItem>` and let the sample demonstrate the pattern.

```csharp
internal sealed class DiscordOpenAITemplatingTransmuter : ITransmuter<IOpenAIPromptEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(IOpenAIPromptEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIEfferent u => Task.FromResult(
                ResponseItem.CreateUserMessageItem($"[discord username:{u.Sender}] {u.Text}")),
            OpenAIAfferent a => Task.FromResult(
                ResponseItem.CreateAssistantMessageItem($"[assistant:{a.Model}] {a.Text}")),
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
    }
}
```

### Alternative: Inline Type Check (No New Interface)

If adding `IOpenAIPromptEntry` feels heavyweight, the filtering can be done inline without a marker interface:

```csharp
// DefaultOpenAITranscriptBuilder.cs
await foreach ((_, OpenAIEntry entry) in _journal.ReadBackwardAsync(...))
{
    if (entry is OpenAIEfferent or OpenAIAfferent)
    {
        ResponseItem item = await _entryToItem.Transmute(entry, cancellationToken);
        buffer.Add(item);
    }
    ...
}
```

And the transmuter throws for unexpected types:

```csharp
internal sealed class OpenAIEntryToResponseItemTransmuter : ITransmuter<OpenAIEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(OpenAIEntry Input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Input switch
        {
            OpenAIEfferent u => Task.FromResult(ResponseItem.CreateUserMessageItem(u.Text)),
            OpenAIAfferent a => Task.FromResult(ResponseItem.CreateAssistantMessageItem(a.Text)),
            _ => throw new ArgumentOutOfRangeException(nameof(Input), 
                $"Cannot transmute {Input.GetType().Name} to ResponseItem. Filter before transmuting.")
        };
    }
}
```

This approach is simpler but pushes the filtering responsibility to every call site.

### Recommended Approach

**Use the marker interface approach (`IOpenAIPromptEntry`)**:

1. It makes the contract explicit — the transmuter only accepts prompt-participating entries
2. The type system prevents callers from passing wrong entry types
3. Filtering happens once at the boundary (in `DefaultOpenAITranscriptBuilder`), not implicitly in every transmuter
4. New transmuters that customize prompt building only need to implement `ITransmuter<IOpenAIPromptEntry, ResponseItem>`

## Why Not Use Existing Constructs?

### Why not IShatterPolicy?

`IShatterPolicy<TEntry>` returns `IEnumerable<TEntry>` — same type in, same type out. It's designed for splitting one entry into multiple (e.g., paragraphs). Using it to filter by returning empty would work but:
- Semantically wrong: shatter means "split", not "filter"
- Type constraint: can't go from `OpenAIEntry` to `ResponseItem`

### Why not IWindowPolicy?

`IWindowPolicy<TChunk>` controls **when** to emit buffered content, not **what** to include. Using `ShouldEmit()` for filtering would conflate timing with inclusion.

### Why not a new IFilterPolicy?

We could introduce:

```csharp
public interface IFilterPolicy<TEntry>
{
    bool ShouldInclude(TEntry entry);
}
```

However, this adds a new abstraction when LINQ predicates already exist. The filtering needed here is simple type-based filtering (`entry is IOpenAIPromptEntry`), which doesn't warrant a new interface.

**If filtering logic becomes complex or reusable**, then `IFilterPolicy<T>` may be warranted. For now, inline type checks or a marker interface suffice.

## Implementation Checklist

### Phase 1: Add Constraint to ITransmuter

- [x] Add `where TOut : notnull` constraint to `ITransmuter<TIn, TOut>` in [ITransmuter.cs](../src/Coven.Transmutation/ITransmuter.cs)
- [x] Add same constraint to `IBiDirectionalTransmuter<TIn, TOut>` (also added `where TIn : notnull` for symmetry)
- [x] Add constraint to `IBatchTransmuter<TChunk, TOutput>` and `BatchTransmuteResult<TChunk, TOutput>`
- [x] Add constraint to `IImbuingTransmuter<TIn, TReagent, TOut>` and `LambdaTransmuter<TIn, TOut>`
- [x] Update XML documentation to reflect "pure transform" expectation

### Phase 2: Create Marker Interface

- [~] ~~Add `IOpenAIPromptEntry` interface~~ — **Skipped**: Chose simpler inline type check approach (see [Implementation Notes](#implementation-notes))

### Phase 3: Migrate Transmuters

- [x] Update `OpenAIEntryToResponseItemTransmuter` to `ITransmuter<OpenAIEntry, ResponseItem>` (throws for unsupported types)
- [x] Update `DiscordOpenAITemplatingTransmuter` similarly
- [x] Update DI registrations in `OpenAIAgentsServiceCollectionExtensions`

### Phase 4: Update Callers

- [x] Update `DefaultOpenAITranscriptBuilder` to filter entries before transmuting
- [x] Remove null checks from transcript building code

### Phase 5: Documentation

- [x] Update [Coven.Transmutation/README.md](../src/Coven.Transmutation/README.md) to document the "pure transform" principle
- [x] Add example showing filter-before-transmute pattern
- [x] Update root README.md and Coven.Agents.OpenAI/README.md

## Decision Summary

| Question | Answer |
|----------|--------|
| Add `where TOut : notnull` constraint? | **Yes** — transmuters should be pure transforms |
| Introduce `Option<T>` or `Result<T, E>`? | **No** — the issue is filtering, not error handling |
| Create new `IFilterPolicy<T>`? | **No** — LINQ predicates and inline type checks suffice |
| Create new `ITransmuterPredicate<T>`? | **No** — too much ceremony for simple type filtering |
| Use marker interface (`IOpenAIPromptEntry`)? | **No** — inline type check chosen for simplicity |

## Implementation Notes

### Why Inline Type Check Instead of Marker Interface?

The proposal originally recommended introducing `IOpenAIPromptEntry` as a marker interface. After implementation, we chose the simpler **inline type check** approach for these reasons:

1. **Fewer moving parts**: No new interface to maintain or document
2. **Explicit filtering**: The `entry is OpenAIEfferent or OpenAIAfferent` check in `DefaultOpenAITranscriptBuilder` is immediately clear
3. **Sufficient for current scope**: Only one call site needs to filter; if more emerge, we can revisit
4. **Exception as documentation**: The `ArgumentOutOfRangeException` clearly states supported types and the filtering expectation

The marker interface approach remains valid and could be adopted later if:
- Multiple call sites need to filter the same types
- Custom transmuters proliferate and need compile-time safety
- The set of prompt-participating entry types grows

### Additional Constraint: `where TIn : notnull` on IBiDirectionalTransmuter

`IBiDirectionalTransmuter<TIn, TOut>` received both `where TIn : notnull` and `where TOut : notnull` constraints because in bidirectional transmutation, `TIn` becomes the output of `TransmuteEfferent`. For symmetry and consistency, both directions should produce non-null values.

## Conclusion

Adding `where TOut : notnull` to `ITransmuter` is **implemented**. The constraint:

1. **Enforces purity**: Transmuters transform, they don't filter
2. **Eliminates ambiguity**: No more guessing what null means
3. **Simplifies callers**: No null checks needed after transmutation
4. **Aligns with existing patterns**: Other transmuters already return non-null or throw

The migration used the simpler inline type check approach:
1. Transmuters accept the base type (`OpenAIEntry`) but throw for unsupported variants
2. Callers filter explicitly before transmuting (`entry is OpenAIEfferent or OpenAIAfferent`)
3. No new marker interface needed

This keeps filtering explicit and testable while transmuters remain pure transforms.
