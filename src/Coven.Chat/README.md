# Coven.Chat

Branch abstraction for multi‑user chat. Defines typed chat entries, batch transmutation for chunked output, and DI helpers for chat windowing.

## What’s Inside

- Entries: `ChatAfferent`, `ChatEfferent`, `ChatEfferentDraft`, `ChatChunk`, `ChatStreamCompleted`, `ChatAck`.
- Windowing: DI extension `AddChatWindowing()` registers a windowing daemon for chat.
- Policies: `Windowing/*` paragraph/length/sentence window policies.
- Shattering: `Shattering/*` policies to split drafts into chunks (paragraph, sentence, max length).
- Transmuter: `ChatChunkBatchTransmuter` (chunks → `ChatEfferent`).

## Why use it?

- Write app logic once against `ChatEntry` and swap leaves (Console, Discord) without changing your code.
- Streamed responses become user‑visible messages based on semantic windowing policies.

## Usage

```csharp
using Coven.Chat;
using Coven.Core.Streaming;

// Register chat windowing in DI
services.AddChatWindowing();

// Optionally override the window policy (OR composition)
services.AddScoped<IWindowPolicy<ChatChunk>>(_ =>
    new CompositeWindowPolicy<ChatChunk>(
        new ChatParagraphWindowPolicy(),
        new ChatMaxLengthWindowPolicy(2000)));
```

## Entries at a Glance

- Afferent (inbound to spine): `ChatAfferent`, `ChatAfferentDraft`, `ChatChunk`, `ChatStreamCompleted`.
- Efferent (outbound to users): `ChatEfferentDraft` (draft), `ChatEfferent` (final), `ChatAck` (local loop prevention).

## See Also

- Adapters: `Coven.Chat.Discord`, `Coven.Chat.Console`.
- Architecture: Abstractions and Branches; Windowing and Shattering.
