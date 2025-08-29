# Coven.Chat.Adapter.Discord — Design (MVP, In‑Memory Journal)

**Status**: Draft v0.1  
**Updated**: 2025‑08‑29  
**Goal**: A practical Discord adapter that wires Coven’s journal‑centric chat model into a running Discord bot using **in‑memory** journal + checkpoints.

This design instantiates the abstractions defined in **Coven.Chat** (TranscriptRef, IChatDelivery, optional ingress/router) and **Coven.Chat.Journal** (entries, pump, checkpoints, awaitables).  

---

## 1) What we’re building

A small package `Coven.Chat.Adapter.Discord` that provides:

1. **DiscordDelivery** → implements `IChatDelivery`. Sends/appends/updates messages in the correct thread; coalesces by `UpdateKey`; idempotent by `(correlationId:seq)`. 
2. **DiscordIngress** → verifies Discord interaction signatures and normalizes payloads to `InboundMessage`. For component interactions (button clicks/modals), it appends a `HumanResponseEntry` to the journal.   
3. **DiscordChatJournalReader** (provider‑aware reader) → maps journal `AskEntry` to interactive Discord messages (buttons) by embedding the journal `CallId` in component IDs; maps Thought/Progress/Reply/Completed/Error to outbound changes. (We override the core reader for richer asks; the rest is identical.)
4. **Transcript factory** host utility → creates/locates the Discord thread for an invocation and binds it via `IInvocationBinder`. 

**Assumptions for MVP:**  
- The host uses **in‑memory** `IAgentJournalStore` and `ICheckpointStore`, runs `JournalPump` on each append or on an interval.
- Commands are registered in Discord (out of band) to provide clean slash UX (`/coven …`).

---

## 2) Types we reuse (from core docs)

- `TranscriptRef` for “where to post” (Endpoint=`"discord:<botNameOrId>"`, Place=`<channelId or threadId>`, RootMessageId=`message id of thread root`). 
- `OutboundChange { Mode: Append|Update, Text, UpdateKey, RenderKind, Meta }`. 
- Journal entries & awaitables (`ThoughtEntry`, `ProgressEntry`, `AskEntry(callId, HumanAsk)`, `HumanResponseEntry(callId, …)`, etc.). 

---

## 3) DiscordDelivery (IChatDelivery)

Implements the adapter port to apply `OutboundChange` to Discord.

```csharp
public sealed class DiscordDelivery : IChatDelivery
{
    private readonly IDiscordApi _api;
    private readonly ConcurrentDictionary<string, bool> _seen = new(); // idempotencyKey → true
    // (endpoint, place, rootMessageId, updateKey) → last message id we can update
    private readonly ConcurrentDictionary<(string ep, string place, string root, string key), string> _updates = new();

    public async ValueTask ApplyAsync(TranscriptRef where, OutboundChange change, string idempotencyKey, CancellationToken ct)
    {
        if (!_seen.TryAdd(idempotencyKey, true)) return; // idempotent

        var key = (where.Endpoint, where.Place, where.RootMessageId, change.UpdateKey ?? "");
        switch (change.Mode)
        {
            case DeliveryMode.Append:
            {
                var msg = await _api.SendAsync(where.Place, change.Text, ComponentsFor(change), ct);
                if (change.UpdateKey is not null)
                    _updates[key] = msg.Id;
                break;
            }
            case DeliveryMode.Update:
            {
                if (change.UpdateKey is not null && _updates.TryGetValue(key, out var msgId))
                {
                    await _api.EditAsync(where.Place, msgId, change.Text, ComponentsFor(change), ct);
                }
                else
                {
                    // Degrade to append if we have no prior message to update (e.g., after restart)
                    var msg = await _api.SendAsync(where.Place, change.Text, ComponentsFor(change), ct);
                    if (change.UpdateKey is not null)
                        _updates[key] = msg.Id;
                }
                break;
            }
        }
    }

    private static DiscordComponents? ComponentsFor(OutboundChange change)
    {
        if (!string.Equals(change.RenderKind, "ask", StringComparison.OrdinalIgnoreCase)) return null;
        if (change.Meta is null) return null;

        // Expect: Meta["callId"], Meta["options"] as CSV or JSON, and maybe Meta["style:opt"]
        var callId = change.Meta.TryGetValue("callId", out var cid) ? cid : null;
        var options = change.Meta.TryGetValue("options", out var opts) ? SplitOptions(opts) : Array.Empty<string>();
        return DiscordComponents.Buttons(
            options.Select(o => (label: o, customId: $"coven|ask|{callId}|{o}")).ToArray());
    }

    private static IReadOnlyList<string> SplitOptions(string raw)
        => raw.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
```

**Notes**

- **Idempotency**: Enforced at adapter edge via `_seen` keyed by `correlationId:seq`. (The Chat reader already supplies that.) 
- **Coalescing**: Uses `UpdateKey` map per transcript; if the map is empty (e.g., after process restart), we gracefully **append** instead of update. This matches the core contract. 
- **Components**: For asks, we render Discord buttons whose `custom_id` encodes the `callId` and the chosen option. The ingress will decode this and write a `HumanResponseEntry`. 

`IDiscordApi` is a tiny abstraction over the Discord REST calls you use (send/edit). You may implement it on top of your preferred HTTP client.

---

## 4) DiscordChatJournalReader (provider‑aware mapping)

We keep the core reader behavior for Thought/Progress/Reply/Completed/Error, but for **Ask** we include `callId` and `options` in `OutboundChange.Meta` so `DiscordDelivery` can render interactive buttons.

```csharp
public sealed class DiscordChatJournalReader : IJournalReader
{
    private readonly ITranscriptIndex _index;
    private readonly IChatDelivery _delivery;

    public string ReaderId => "chat";

    public DiscordChatJournalReader(ITranscriptIndex index, IChatDelivery delivery)
    { _index = index; _delivery = delivery; }

    public async ValueTask OnRecordAsync(JournalRecord record, CancellationToken ct)
    {
        if (!_index.TryGet(record.CorrelationId, out var where)) return;

        var change = Map(record.Entry);
        if (change is null) return;

        var idempotencyKey = $"{record.CorrelationId}:{record.Seq}";
        await _delivery.ApplyAsync(where, change, idempotencyKey, ct);
    }

    private static OutboundChange? Map(AgentEntry e) => e switch
    {
        ThoughtEntry t   => new OutboundChange(DeliveryMode.Update, $"🧠 {t.Text}", UpdateKey: t.CoalesceKey, RenderKind: "thought"),
        ProgressEntry p  => new OutboundChange(DeliveryMode.Update, FormatProgress(p), UpdateKey: p.CoalesceKey, RenderKind: "progress"),
        ReplyEntry r     => new OutboundChange(DeliveryMode.Append, r.Text, RenderKind: "reply"),
        AskEntry a       => new OutboundChange(
                                DeliveryMode.Append,
                                FormatAsk(a),
                                UpdateKey: a.CoalesceKey,
                                RenderKind: "ask",
                                Meta: new Dictionary<string,string> {
                                    ["callId"] = a.CallId.ToString("N"),
                                    ["options"] = a.Ask.Options is { Count: >0 } opts ? string.Join(",", opts) : ""
                                }),
        CompletedEntry c => new OutboundChange(DeliveryMode.Append, "✅ Completed.", RenderKind: "completed"),
        ErrorEntry err   => new OutboundChange(DeliveryMode.Append, $"🛑 {err.Message}", RenderKind: "error"),
        _ => null
    };

    private static string FormatProgress(ProgressEntry p)
    {
        var pct = p.Percent is null ? "" : $" {(int)System.Math.Round((p.Percent.Value)*100)}%";
        var stage = string.IsNullOrWhiteSpace(p.Stage) ? "" : $" — {p.Stage}";
        var text = string.IsNullOrWhiteSpace(p.Text) ? "" : $": {p.Text}";
        return $"⏳{pct}{stage}{text}".Trim();
    }

    private static string FormatAsk(AskEntry a)
        => a.Ask.Options is { Count: >0 } opts
            ? $"❓ {a.Ask.Prompt}  Options: {string.Join(", ", opts)}"
            : $"❓ {a.Ask.Prompt}";
}
```

This reader preserves the **core contract** (per‑correlation order, idempotency key) while enriching asks enough to build Discord interactions. 

---

## 5) DiscordIngress (normalize incoming → journal)

A minimal HTTP handler that processes Discord interactions:

- **Verify signatures** using the app’s public key. Reject if invalid.  
- **Slash commands / messages** → produce an `InboundMessage` `{ Endpoint="discord:<bot>", Place="<channelId>", Sender="<userId>", Text, Args }` for your `IChatRouter`. If routing succeeds, the host will **create a transcript** (see §6) and start the ritual. 
- **Component interactions (buttons)** with `custom_id = "coven|ask|{callId}|{option}"` → append a `HumanResponseEntry(callId, new HumanResponse { Selected = option, Fields = null, Responder = ... })` via `IAgentJournalStore.AppendAsync(...)`. Awaiters (`AskAwaitable.Response`) will complete automatically. 

Sketch:

```csharp
public sealed class DiscordIngress : IChatIngress
{
    private readonly IAgentJournalStore _store;

    public bool TryParse(object transport, out InboundMessage inbound)
    {
        // transport carries headers/body; verify signature & timestamp
        // decode to interaction model; map to InboundMessage or handle component
        inbound = default!; return false; // impl specific
    }

    public async Task<bool> TryHandleComponentAsync(DiscordComponentInteraction i, CancellationToken ct)
    {
        // custom_id: "coven|ask|{callId}|{option}"
        var parts = i.Data.CustomId.Split('|');
        if (parts.Length == 4 && parts[1] == "ask" && Guid.TryParseExact(parts[2], "N", out var callId))
        {
            var corr = ExtractCorrelationId(i); // from thread mapping or message metadata
            await _store.AppendAsync(corr,
                new HumanResponseEntry(callId, new HumanResponse { Selected = parts[3]!, Responder = new IdentityRef(i.User.Id.ToString(), i.User.Username) }, DateTimeOffset.UtcNow), ct);
            return true;
        }
        return false;
    }
}
```

> The correlation can be looked up via a small host map: `(thread/channel id → correlationId)` saved when the ritual starts (see §6).

---

## 6) Transcript creation & binding (host utility)

When routing from a Discord command/message:

1. **Create a thread** in the target channel or reuse the message’s thread.  
2. **Post a root message** like “Starting…” and get its `messageId`.  
3. Build `TranscriptRef(endpoint="discord:<bot>", place="<threadId or channelId>", rootMessageId="<messageId>")`.  
4. `IInvocationBinder.Bind(correlationId, transcriptRef)`.  
5. Start the ritual. All journal events now flow to this thread via `DiscordChatJournalReader` + `DiscordDelivery`. 

For in‑memory hosts, keep a `ConcurrentDictionary<string, Guid>` mapping `{place/threadId → correlationId}` so ingress can resolve which correlation to append responses to.

---

## 7) DI for in‑memory journaling (reference)

```csharp
// Journal (in-memory)
services.AddSingleton<IAgentJournalStore, InMemoryAgentJournalStore>();
services.AddSingleton<ICheckpointStore, InMemoryCheckpointStore>();
services.AddSingleton<IJournalBarrier, DefaultJournalBarrier>();
services.AddSingleton<IJournalWaiter, DefaultJournalWaiter>();
services.AddScoped<IMagikJournal>(sp => new DefaultMagikJournal(
    sp.GetRequiredService<IAgentJournalStore>(),
    sp.GetRequiredService<IJournalBarrier>(),
    sp.GetRequiredService<IJournalWaiter>(),
    correlationId: ExecutionScope.CurrentCorrelationId)); // engine-scoped
// Readers
services.AddSingleton<IInvocationBinder, InMemoryInvocationBinder>(); // also ITranscriptIndex
services.AddSingleton<IChatDelivery, DiscordDelivery>();
services.AddSingleton<IJournalReader, DiscordChatJournalReader>();
services.AddSingleton<JournalPump>();
// Adapter
services.AddSingleton<IDiscordApi, DiscordRestApi>();     // thin REST wrapper
services.AddSingleton<IChatIngress, DiscordIngress>();
// Router (optional): map InboundMessage → ritual
services.AddSingleton<IChatRouter, SimpleChatRouter>();
```

This matches the core DI pattern in the docs while swapping in our Discord adapter pieces.  

---

## 8) End‑to‑end flows

### 8.1 Start from slash command

1. Discord sends interaction → `DiscordIngress.TryParse` → `InboundMessage`.  
2. Host router → ritual plan.  
3. Host creates thread & binds `TranscriptRef` via `IInvocationBinder`.  
4. Engine starts ritual; agent writes to journal.  
5. `JournalPump` drains; `DiscordChatJournalReader` → `OutboundChange`; `DiscordDelivery` sends/edits messages idempotently.  

### 8.2 Human‑in‑the‑loop

1. Agent appends `AskEntry(callId, HumanAsk{ Options=[…] })`.  
2. `DiscordChatJournalReader` emits `OutboundChange(RenderKind="ask", Meta{ callId, options })`; `DiscordDelivery` posts buttons with `custom_id` carrying `callId`.  
3. User clicks → `DiscordIngress.TryHandleComponentAsync` appends `HumanResponseEntry(callId, …)`.  
4. Agent’s `AskAwaitable.Response()` completes; ritual continues. Compaction may later prune the answered request per policy. 

---

## 9) Operational notes (MVP)

- **Rate limits**: rely on Discord’s HTTP 429 headers; `IDiscordApi` should queue/retry with backoff per route. Coalescing at the journal/reader level already reduces chatter (Update vs Append). 
- **Idempotency**: duplicates (`correlationId:seq`) are ignored in `DiscordDelivery`.  
- **Recoverability**: if `DiscordDelivery` loses its `UpdateKey→messageId` map (process restart), “Update” degrades to an “Append” and continues safely; the user may see a new line, which is acceptable under the core contract. 
- **Security**: verification of interaction signatures is mandatory; accept only from your app ID.  
- **Visibility**: use regular thread messages for shared updates; if you later support private/ephemeral asks, keep that logic inside `IDiscordApi` and do not change the journal contracts.

---

## 10) Testing

- **FakeDiscordApi** to capture `SendAsync/EditAsync` calls for assertions.  
- **Reader mapping** tests: `AskEntry` → `OutboundChange(RenderKind="ask", Meta{callId, options})`.  
- **Idempotency**: send the same `(corr, seq)` twice; assert `IDiscordApi` receives a single call.  
- **Update fallback**: clear `_updates` and replay an “Update”; assert it appends instead.  
- **Ingress**: component interaction with `custom_id` → `HumanResponseEntry` appended; waiter completes. 

---

## 11) Minimal IDiscordApi (shape)

```csharp
public interface IDiscordApi
{
    Task<DiscordMessage> SendAsync(string placeId, string text, DiscordComponents? components, CancellationToken ct);
    Task<DiscordMessage> EditAsync(string placeId, string messageId, string text, DiscordComponents? components, CancellationToken ct);
}

public sealed record DiscordMessage(string Id);

public abstract record DiscordComponents
{
    public static DiscordComponents Buttons(params (string Label, string CustomId)[] items) => new ButtonRow(items);
    public sealed record ButtonRow(IReadOnlyList<(string Label, string CustomId)> Items) : DiscordComponents;
}
```

This interface is intentionally tiny; any HTTP client/lib can implement it.

---

## 12) Non‑Goals for MVP

- No modal forms; only button options for asks. (Forms can be added later by extending Meta and Ingress.)  
- No persistence beyond in‑memory journal/checkpoints.  
- No dynamic command registration; we assume commands are pre‑registered in Discord.

---