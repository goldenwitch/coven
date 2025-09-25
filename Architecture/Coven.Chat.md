# Coven.Chat

Lightweight chat abstractions for Daemon IO using Coven.Core journaling (`IScrivener`).

- Namespace: `Coven.Chat`
- Core: `IScrivener<ChatEntry>` (append, tail, wait) and chat entry types (e.g., `ChatThought`, `ChatResponse`)
- Goal: Make message exchange deterministic and testable without prescribing a transport

---

## Principles

- Typed entries: `ChatEntry` is the exact entry type your app exchanges.
- DI‑first: Resolve `IScrivener<ChatEntry>` and any hosts via your container. Implementations use constructor injection.
- Storage‑agnostic: Sequence numbers, timestamps, and I/O are implementation details; your code sees `ChatEntry` and returned positions for chaining.

---

## Usage Patterns

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
