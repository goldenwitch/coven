# Coven.Core.Streaming

Windowing and shattering primitives for streaming journals. Turn a flow of chunks into well‑formed outputs under explicit policies, with a daemon that handles buffering, emit timing, and final flush.

## What’s Inside

- IWindowPolicy<TChunk>: decide when a buffered window should emit
- StreamWindow<TChunk>: snapshot passed to window policies
- CompositeWindowPolicy<TChunk>: OR‑composition of policies
- LambdaWindowPolicy<TChunk>: delegate‑based policy
- IShatterPolicy<TEntry>: split one entry into zero or more entries
- LambdaShatterPolicy<TEntry>: delegate‑based shatter
- ChainedShatterPolicy<TEntry>: sequential shatter pipeline
- StreamWindowingDaemon<TEntry,TChunk,TOutput,TCompleted>: generic daemon that windows a journal and emits outputs

Depends on Coven.Transmutation for batch transmutation:

- IBatchTransmuter<TChunk,TOutput> — converts a batch of chunks into an output (+ optional remainder)
- BatchTransmuteResult<TChunk,TOutput> — output + remainder contract

## Why use it?

- Stream control: shape how incremental chunks become user‑visible outputs.
- Deterministic flush: completion markers drain remaining buffered data.
- Composable: combine policies via `CompositeWindowPolicy` to meet UX needs.
- Extensible: adapt any entry types; not tied to chat or agents.

## Key Concepts

- Chunk vs Output:
  - Chunks (`TChunk`) are the granular pieces appended to a journal during streaming.
  - Outputs (`TOutput`) are finalized entries emitted when a window policy decides to emit.
- Directionality in practice:
  - Policies typically operate on afferent chunks (incoming toward the spine) and emit efferent outputs (outbound to users/adapters), but the primitives are direction‑agnostic.
- Window Policy (`IWindowPolicy<TChunk>`):
  - `MinChunkLookback`: ensures policy decisions see enough recent context.
  - `ShouldEmit(StreamWindow<TChunk>)`: returns true to emit at the current point.
- Shatter Policy (`IShatterPolicy<TEntry>`):
  - Optionally breaks an output into zero or more entries (e.g., paragraphs).
  - If no shards are produced, the original output is forwarded as‑is.
- Completion (`TCompleted`):
  - A special entry that triggers a full drain of the buffer.
  - The daemon emits as many outputs as needed to flush remaining chunks.

### Semantic Windowing

Policies model readiness, not fixed “turns.” A window is emitted when it’s semantically ready (e.g., paragraph boundary, safe length cap, debounce, or explicit marker). See architecture: `architecture/Windowing-and-Shattering.md`.

## How StreamWindowingDaemon Works

Given a journal of `TEntry` (where `TChunk`, `TOutput`, and `TCompleted` are subtypes of `TEntry`):

1) On Start, the daemon tails the journal after the latest position and sets `Status.Running`.
2) It buffers `TChunk` entries as they arrive. For each new chunk:
   - It constructs a `StreamWindow<TChunk>` consisting of the last `MinChunkLookback` chunks, total chunk count, start time, and last emit time.
   - If the window policy returns true, it batch‑transmutes the buffer via `IBatchTransmuter<TChunk,TOutput>`.
   - If a shatter policy is provided, it writes each shard; otherwise it writes the transmuted output.
   - If the transmuter returns a remainder chunk, the buffer becomes only the remainder; else it clears.
3) On a `TCompleted` entry, it drains the buffer completely:
   - Repeatedly batch‑transmute and write outputs until the buffer is empty (guarded against infinite loops).
4) On Shutdown, it cancels, awaits the pump, and sets `Status.Completed`.
5) On unexpected exceptions, it calls `Fail(ex)` so orchestration can react.

## Usage Examples

### Minimal policy with final‑only emit

```csharp
using Coven.Core;
using Coven.Core.Streaming;
using Coven.Transmutation;

// Emit only when a completion marker arrives (final‑only)
IWindowPolicy<MyChunk> policy = new LambdaWindowPolicy<MyChunk>(1, _ => false);

// Simple batch transmuter: concatenate chunk text
public sealed class MyBatchTransmuter : IBatchTransmuter<MyChunk, MyOutput>
{
    public Task<BatchTransmuteResult<MyChunk, MyOutput>> Transmute(IEnumerable<MyChunk> input, CancellationToken ct = default)
    {
        string text = string.Concat(input.Select(c => c.Text));
        return Task.FromResult(new BatchTransmuteResult<MyChunk, MyOutput>(
            new MyOutput(text),
            HasRemainder: false,
            Remainder: default));
    }
}

// Wire daemon (e.g., in DI factory)
var daemon = new StreamWindowingDaemon<MyEntry, MyChunk, MyOutput, MyCompleted>(
    daemonEvents: myDaemonEventScrivener,
    journal: myJournal,
    windowPolicy: policy,
    batchTransmuter: new MyBatchTransmuter(),
    shatterPolicy: null);
```

### Composite policies (paragraphs OR max length)

```csharp
// Combine multiple emit rules via OR
IWindowPolicy<MyChunk> policy = new CompositeWindowPolicy<MyChunk>(
    new LambdaWindowPolicy<MyChunk>(minLookback: 2, window =>
    {
        // Example: emit on blank‑line paragraph boundary
        return window.PendingChunks.Any(c => string.IsNullOrWhiteSpace(c.Text));
    }),
    new LambdaWindowPolicy<MyChunk>(minLookback: 1, window =>
    {
        // Example: emit when total buffered text exceeds N characters
        return window.PendingChunks.Sum(c => c.Text.Length) >= 1000;
    }));
```

### Shattering outputs

```csharp
// Split an output into multiple entries (e.g., paragraphs)
IShatterPolicy<MyEntry> shatter = new LambdaShatterPolicy<MyEntry>(entry =>
{
    if (entry is MyOutput o)
    {
        return o.Text
            .Split("\n\n")
            .Select(p => new MyOutputParagraph(p));
    }
    return Array.Empty<MyEntry>();
});
```

### Chat Example (built‑in)

`Coven.Chat` wires a windowing daemon for chat journals:

```csharp
services.AddChatWindowing();

// Internally registers:
// new StreamWindowingDaemon<ChatEntry, ChatChunk, ChatEfferent, ChatStreamCompleted>(...)
// Policy defaults to final‑only (emit on completion) unless overridden via DI
```

You can override the chat window policy by registering your own `IWindowPolicy<ChatChunk>` (or chain policies via `CompositeWindowPolicy<ChatChunk>`).

### OpenAI Example (policy ideas)

`Coven.Agents.OpenAI` includes ready‑made policies (e.g., paragraph, max‑length, thought windowing) that you can mix and match using `CompositeWindowPolicy<TChunk>` to tune when agent responses and thoughts are emitted.

## Tips

- Choose `MinChunkLookback` to balance responsiveness and context.
- Use remainders when your batch transmuter only consumes part of the last chunk.
- Provide a `TCompleted` entry to guarantee all buffered content is emitted.
- Prefer pure policies (no side‑effects) for predictability and testability.
- Ensure a single `IScrivener<TEntry>` instance is used for a given flow.
 - Be mindful of overhead: windowing/shattering daemons introduce buffering and journaling work. For hot paths, apply window/shatter inline (mid‑process) without a daemon where it makes the most performance sense.

## Testing

- Use `InMemoryScrivener<T>` (from `Coven.Core`) to unit‑test daemon behavior.
- Assert emission timing by appending `TChunk` entries and awaiting journal tails.
- Verify full flush by appending `TCompleted` and observing outputs.

## See Also

- Coven.Transmutation: `IBatchTransmuter`, `BatchTransmuteResult`, `ITransmuter`
- Coven.Chat: wiring example and default batch transmuter for chat
- Root README: window/shatter overview and end‑to‑end samples
