# Coven.Ui.Shell

Application-level journal for Coven user interfaces. Carries agent reasoning and app notices so they stay out of the chat transcript.

## What's Inside

- Entries: `UiThought` (reasoning), `UiNotice` (app events with a `UiNoticeLevel`).
- DI: `AddUiShell()` registers `IScrivener<UiEntry>` if the host has not supplied one.
- Manifest: `UseUiShell()` for declarative covenants.

## Why a Second Journal

A `BranchManifest` carries exactly one `JournalEntryType`. Chat conversation belongs in `ChatEntry`; reasoning and notices belong somewhere else, or they end up rendered as chat messages.

## Usage

```csharp
BranchManifest shell = coven.UseUiShell();
```

`UiThought` is consumed, so the covenant declares a route producing it — typically from `AgentThought`, which samples otherwise mark terminal and discard:

```csharp
c.Route<AgentThought, UiThought>(
    (t, ct) => Task.FromResult(new UiThought(t.Sender, t.Text)));
```

`UiNotice` is produced by the application writing directly to the journal, so the covenant marks it terminal:

```csharp
c.Terminal<UiNotice>();
```

## See Also

- Leaf: `Coven.Chat.Ui` for the conversation itself.
- App: `src/apps/Coven.Ui.Desktop`.
