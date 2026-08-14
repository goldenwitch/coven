# Coven.Chat.Ui

In-process UI chat adapter (leaf). Bridges `Coven.Chat` entries to a user interface through a UI-framework-agnostic channel.

## What's Inside

- Config: `UiChatClientConfig` (`InputSender`, `OutputSender`).
- Channel: `IUiChannel` / `UiChannel` — the in-process transport between the leaf and a UI.
- Gateway + Session: connect the channel to the chat journal.
- Transmuter: `UiChatTransmuter` (`UiChatEntry` ↔ `ChatEntry`).
- Journals: `IScrivener<UiChatEntry>`, `IScrivener<ChatEntry>`.
- Daemon: `UiChatDaemon`.

## Usage

```csharp
using Coven.Chat.Ui;

services.AddUiChat(new UiChatClientConfig
{
    InputSender = "user",
    OutputSender = "BOT"
});
```

For declarative covenants, `UseUiChat` returns a manifest:

```csharp
BranchManifest chat = coven.UseUiChat(uiConfig, reg => reg.EnableStreaming());
```

## Streaming

Without `EnableStreaming()`, the branch consumes only `ChatEfferent` — finalized messages.

With `EnableStreaming()`, it also consumes `ChatChunk`, so tokens render as they arrive ahead of the windowed `ChatEfferent`. The covenant must then declare a route producing `ChatChunk`, typically from `AgentAfferentChunk`.

A UI receiving both renders chunks into a pending message and replaces it when the finalized `ChatEfferent` arrives.

## Threading

`UiChannel.Outbound` is raised on the thread that produced the payload — a background journal pump, never a UI thread. Handlers marshal to their own dispatcher.

`IUiChannel` is registered as a singleton so a host keeps one reference across ritual scopes.

## See Also

- Branch: `Coven.Chat` for windowing.
- Adapters: `Coven.Chat.Console`, `Coven.Chat.Discord`.
- App: `src/apps/Coven.Ui.Desktop`.
