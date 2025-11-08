# Windowing and Shattering

Control how streaming chunks become user‑visible outputs. Policies decide when to emit; shatter rules split outputs for better UX.

## Concepts
- Chunks: granular streaming fragments written to journals (e.g., `AgentAfferentChunk`).
- Window: buffered view over pending chunks, passed to `IWindowPolicy<TChunk>`.
- Emit: when a policy returns true, buffered chunks are batch‑transmuted into an output.
- Shatter: optional split of an output into multiple entries (e.g., paragraphs).
- Completion: a marker entry causes the buffer to flush deterministically.

## Semantic Windowing

Instead of fixed “turns,” Coven supports semantic windowing: policies determine when a buffered window of incoming messages is ready for decision‑making. Readiness is about meaning, not sequence count.

- Readiness examples:
  - Content boundary reached (paragraph/sentence end, code block closed).
  - Thought summary/marker emitted indicating a coherent chunk is available.
  - Safety thresholds crossed (token/length caps) to prevent over‑accumulation.
  - Time‑based debounce to avoid emitting on every tiny chunk while staying responsive.
  - External/user signals (e.g., user stop, domain event) indicating a decision point.

- Decisions at readiness:
  - Emit an interim or final response to the user.
  - Advance workflow (e.g., route to a block, persist a checkpoint).
  - Trigger downstream processing that benefits from coherent input windows.

- Implementing semantics:
  - Encode readiness in `IWindowPolicy<TChunk>`; compose multiple semantics with `CompositeWindowPolicy<TChunk>` (logical OR).
  - Use `IBatchTransmuter<TChunk,TOutput>` to convert a ready window into a decision artifact (e.g., response or structured record); return a remainder if partially consumed.
  - Use completion markers to drain any residual buffered content deterministically.

## Why It Matters
- Responsiveness: stream partial outputs without sacrificing coherence.
- Control: combine policies (e.g., paragraph OR max‑length) to match UX goals.
- Determinism: completion guarantees final flush and consistent replay.

## Typical Policies
- Final‑only: emit only on completion.
- Paragraph‑first: emit on paragraph boundaries.
- Max‑length: emit when buffered size exceeds a threshold.
- Composite: OR multiple policies for flexible behavior.

## Related
- See `src/Coven.Core.Streaming/README.md` for API details and examples.
- Used by Chat and Agents branches to shape streaming UX.
