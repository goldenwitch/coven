# Coven.Chat

Lightweight journaling and chat abstractions for agent and adapter I/O. The journal is conceptual (append‑only log/stream); entry types are concrete and application‑defined.

- Namespace: `Coven.Chat`
- Core: `IScrivener<TEntry>` (append, tail, wait), chat entry types (e.g., `ChatThought`, `ChatResponse`)
- Goal: Make message exchange deterministic and testable without prescribing a transport

---

## Principles

- Typed entries: `TEntry` is the exact entry type your app exchanges (often a small union/base + derived records).
- DI‑first: Resolve `IScrivener<TEntry>` and any hosts via your container. Implementations use constructor injection.
- Append + Tail/Wait: All flows reduce to appending entries and awaiting the next matching entry; forward and backward reads are supported by concrete implementations.
- Storage‑agnostic: Sequence numbers, timestamps, and I/O are implementation details; your code sees `TEntry` and returned positions for chaining.

---

## Usage Patterns

These examples show only code you write. Replace entry types with your own if not using `ChatEntry`.

### Send a message

```csharp
// Given IScrivener<ChatEntry> via DI
await scrivener.WriteAsync(new ChatThought("agent", "Starting up..."), ct);
```

### Wait for the next response

```csharp
long after = 0; // or last anchor position
var (_, response) = await scrivener.WaitForAsync<ChatResponse>(after, ct);
Console.WriteLine($"user said: {response.Text}");
```

### Ask (prompt → await answer)

```csharp
var anchor = await scrivener.WriteAsync(new ChatThought("agent", "How can I help?"), ct);
var (_, reply) = await scrivener.WaitForAsync<ChatResponse>(anchor, ct);
```

### Stream updates to a UI

```csharp
await foreach (var (_, entry) in scrivener.TailAsync(0, ct))
{
    switch (entry)
    {
        case ChatThought t:   ui.ShowThought(t);   break;
        case ChatResponse r:  ui.ShowResponse(r);  break;
        // add project‑specific entries as needed
    }
}
```

### Backward paging (last N entries)

```csharp
var items = new List<ChatEntry>();
await foreach (var (_, entry) in scrivener.ReadBackwardAsync(long.MaxValue, ct))
{
    items.Add(entry);
    if (items.Count == 50) break;
}
items.Reverse(); // chronological
```

---

## Agents and Adapters

- Adapters own the client transport and bind to an `IScrivener<TEntry>`.
- Agents read and write through `IScrivener<TEntry>` without knowing about the transport.
- Separate scriveners (one per side) can safely share a journal if they use the same `TEntry`.

See agent/adapter docs for their wiring; the journal remains the stable contract in the middle.

---

## Testing

- Use an in‑memory scrivener to assert ordering/contents and script responses.
- Prefer matching by type and minimal text patterns; avoid coupling to storage details.

