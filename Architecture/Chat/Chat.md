# Coven Journaling Pattern (3‑type, DI‑friendly core) — updated generics & backward reads

**Scope:** Three public surfaces that match the diagram: `IAdapterHost<TClientMessage, TJournalEntryType>`, `IAgentHost<TClientMessage, TJournalEntryType>`, `IScrivener<TJournalEntryType>`. The **Journal** is conceptual only (the log/stream), not a class.

> Supported patterns: **WaitForMessage**, **SendMessage**, **Ask**.

---

## 1) Principles

* **Journal is conceptual:** storage chooses how to persist entries and metadata.
* **Typed entries:** `TJournalEntryType` **is the journal entry type** your app writes/reads (often a union/base + derived records).
* **DI‑first:** Concrete scriveners/hosts are wired via your container. Implementations **must use constructor injection** to receive their `IScrivener<TJournalEntryType>`. No opinions here about run loops.
* **Append + Tail/Wait:** All flows reduce to appending entries and awaiting the next matching entry (forward **and backward** reads supported).
* **Metadata stays behind the interface:** sequence numbers, timestamps, etc., are store concerns and surface only as return values alongside `TJournalEntryType`. We refer to the location of an entry as its `journalPosition` (a monotonically increasing long).

---

## 2) Contract

### `IScrivener<TJournalEntryType>.cs`

```csharp
namespace Coven.Chat.Journal;

/// <summary>
/// Minimal journal API bound to a single journal stream (binding handled by DI).
/// TJournalEntryType is the message/entry type. No correlation id is exposed.
/// </summary>
public interface IScrivener<TJournalEntryType>
{
    /// <summary>Append one entry; returns the assigned journal position for chaining/awaits.</summary>
    ValueTask<long> WriteAsync(TJournalEntryType entry, CancellationToken ct = default);

    /// <summary>Stream entries with journalPosition > afterPosition (forward).</summary>
    IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> TailAsync(long afterPosition = 0, CancellationToken ct = default);

    /// <summary>Stream entries with journalPosition < beforePosition in descending order (backward).</summary>
    IAsyncEnumerable<(long journalPosition, TJournalEntryType entry)> ReadBackwardAsync(long beforePosition = long.MaxValue, CancellationToken ct = default);

    /// <summary>Wait for the next entry after 'afterPosition' that matches the predicate.</summary>
    ValueTask<(long journalPosition, TJournalEntryType entry)> WaitForAsync(long afterPosition, Func<TJournalEntryType, bool> match, CancellationToken ct = default);

    /// <summary>Convenience: wait for the next entry of a specific derived type.</summary>
    ValueTask<(long journalPosition, TDerived entry)> WaitForAsync<TDerived>(long afterPosition, CancellationToken ct = default)
        where TDerived : TJournalEntryType;
}
```

---

## 3) Test call patterns for `IScrivener<TJournalEntryType>`

> In the snippets below, `TUserMessage`, `TReply`, `TProgress`, `TError`, `TAsk`, `TAnswer`, etc., are **your** concrete types that implement/extend `TJournalEntryType`. The scrivener is obtained via DI (constructor‑injected). Variable names are expanded for clarity.

### A) Basic request → wait for a terminal response

```csharp
public sealed class Handler<TJournalEntryType>(IScrivener<TJournalEntryType> scrivener)
{
    public async Task<string> RequestReplyAsync(TJournalEntryType userMessageEntry, CancellationToken cancellationToken)
    {
        // 1) Append the inbound user message. The returned journalPosition is our durable anchor.
        //    This follows the design's "Append + Wait" model and prevents races.
        long afterPosition = await scrivener.WriteAsync(userMessageEntry, cancellationToken);

        // 2) Wait for the next terminal entry (Reply/Completed/Error) strictly AFTER the anchor.
        //    The predicate works directly on the typed entry (TJournalEntryType) — no payload wrapper.
        var (_, matchingEntry) = await scrivener.WaitForAsync(
            afterPosition,
            entry => entry is TReply || entry is TError || entry is TCompleted,
            cancellationToken);

        // 3) Interpret the terminal entry. Error path throws; others are domain-specific.
        return matchingEntry switch
        {
            TReply replyEntry         => /* map reply to string */ replyEntry.ToString()!,
            TCompleted completedEntry => /* serialize result */ completedEntry.ToString()!,
            TError errorEntry         => throw new Exception(errorEntry.ToString()),
            _                         => throw new InvalidOperationException("Unexpected entry")
        };
    }
}
```

### B) Multi‑turn Ask/Answer (race‑safe via anchor)

```csharp
public async Task<string> AskThenWaitAsync(
    IScrivener<TJournalEntryType> scrivener,
    TAsk askEntry,
    Func<TAnswer, bool> answerMatches,
    CancellationToken cancellationToken)
{
    // 1) Write the Ask entry. Any correlation needed (e.g., callId) is part of your typed entry.
    //    No correlationId API is required.
    long afterPosition = await scrivener.WriteAsync(askEntry, cancellationToken);

    // 2) Anchor-safe wait: if the user answers immediately, we still won't miss it
    //    because we are waiting strictly AFTER the Ask's journalPosition.
    var (_, matchingEntry) = await scrivener.WaitForAsync(
        afterPosition,
        e => e is TAnswer a && answerMatches(a),
        cancellationToken);

    // 3) Return or project the answer as your domain requires.
    return ((TAnswer)matchingEntry).ToString()!;
}
```

### C) Stream updates forward to a UI sink

```csharp
public async Task StreamForwardAsync(
    IScrivener<TJournalEntryType> scrivener,
    long afterPosition,
    IUiSink uiSink,
    CancellationToken cancellationToken)
{
    // 1) Tail forward: emit entries whose journalPosition > afterPosition (strictly newer).
    await foreach (var (_, journalEntry) in scrivener.TailAsync(afterPosition, cancellationToken))
    {
        // 2) UI decides how to coalesce/progress; API doesn't enforce coalescing.
        switch (journalEntry)
        {
            case TProgress progressEntry: uiSink.Progress(progressEntry); break;
            case TReply replyEntry:       uiSink.Reply(replyEntry);       break;
            case TError errorEntry:       uiSink.Error(errorEntry);       break;
        }
    }
}
```

### D) Backward paging: fetch last N entries (newest → oldest → chronological)

```csharp
public async Task<IReadOnlyList<TJournalEntryType>> LastNAsync(
    IScrivener<TJournalEntryType> scrivener,
    int count,
    CancellationToken cancellationToken)
{
    // 1) Read backward starting from the logical end (long.MaxValue).
    var buffer = new List<TJournalEntryType>(count);
    await foreach (var (_, journalEntry) in scrivener.ReadBackwardAsync(long.MaxValue, cancellationToken))
    {
        buffer.Add(journalEntry);
        if (buffer.Count == count) break;
    }
    // 2) Reverse to chronological order (oldest → newest) for display.
    buffer.Reverse();
    return buffer;
```

### E) Bookmark & resume (robust consumer)

```csharp
public async Task ConsumeForeverAsync(
    IScrivener<TJournalEntryType> scrivener,
    IBookmarkStore bookmarkStore,
    CancellationToken cancellationToken)
{
    // 1) Load last committed journalPosition from durable storage.
    long bookmarkPosition = await bookmarkStore.LoadAsync();

    // 2) Resume tailing strictly AFTER the bookmark to avoid duplicates.
    await foreach (var (journalPosition, journalEntry) in scrivener.TailAsync(bookmarkPosition, cancellationToken))
    {
        // 3) Process the entry. Make handling idempotent where possible.
        await HandleAsync(journalEntry, cancellationToken);

        // 4) Advance the bookmark only after successful handling, ensuring at-least-once semantics.
        await bookmarkStore.SaveAsync(journalPosition);
    }
}
```

### F) Wait with timeout (non‑blocking UX)

```csharp
public async Task<TJournalEntryType?> WaitOrNullAsync(
    IScrivener<TJournalEntryType> scrivener,
    long afterPosition,
    TimeSpan timeoutDuration,
    CancellationToken cancellationToken)
{
    // 1) Derive a timeout token to bound the wait. This keeps UIs responsive.
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeoutCts.CancelAfter(timeoutDuration);

    try
    {
        // 2) Anchor-safe wait for "any" next entry. If timed out, return null/default.
        var (_, journalEntry) = await scrivener.WaitForAsync(afterPosition, _ => true, timeoutCts.Token);
        return journalEntry;
    }
    catch (OperationCanceledException)
    {
        return default;
    }
}
```

### G) Idempotent write (simple dedupe by key)

```csharp
public async Task WriteOnceAsync(
    IScrivener<TJournalEntryType> scrivener,
    Func<TJournalEntryType, bool> isDuplicateOf,
    TJournalEntryType entryToWrite,
    CancellationToken cancellationToken)
{
    // 1) Heuristic dedupe: scan backward for a matching entry.
    //    Store implementations could optimize this; the API supports either approach.
    await foreach (var (_, journalEntry) in scrivener.ReadBackwardAsync(long.MaxValue, cancellationToken))
        if (isDuplicateOf(journalEntry)) return; // already present

    // 2) Safe to append.
    await scrivener.WriteAsync(entryToWrite, cancellationToken);
}
```

### H) Typed wait helper (when you know the exact derived type)

```csharp
public async Task<TAnswer> WaitForAnswerAsync<TAnswer>(
    IScrivener<TJournalEntryType> scrivener,
    long afterPosition,
    CancellationToken cancellationToken)
    where TAnswer : TJournalEntryType
{
    // 1) Use the generic convenience overload to wait for a specific derived type.
    //    This aligns with the design's typed-entry model.
    var (_, answerEntry) = await scrivener.WaitForAsync<TAnswer>(afterPosition, cancellationToken);
    return answerEntry;
}
```

### I) Fan‑out processing loop (agent side)

```csharp
public async Task AgentLoopAsync(
    IScrivener<TJournalEntryType> scrivener,
    CancellationToken cancellationToken)
{
    // 1) Tail from the beginning (afterPosition = 0). Multiple consumers can do this concurrently.
    await foreach (var (journalPosition, journalEntry) in scrivener.TailAsync(0, cancellationToken))
    {
        // 2) Fan-out processing if desired. Order-sensitive pipelines may avoid Task.Run.
        _ = Task.Run(async () => await ProcessOneAsync(scrivener, journalPosition, journalEntry, cancellationToken), cancellationToken);
    }
}

private async Task ProcessOneAsync(
    IScrivener<TJournalEntryType> scrivener,
    long afterPosition,
    TJournalEntryType journalEntry,
    CancellationToken cancellationToken)
{
    // 3) Write follow-up entries (Thought/Progress/Reply/etc.). Use 'afterPosition' as the anchor for subsequent waits if needed.
    await scrivener.WriteAsync(/* next entry */, cancellationToken);
}
```
