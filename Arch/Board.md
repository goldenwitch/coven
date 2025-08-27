# Board

The Board is where MagikBlocks find their next work. Each block can only get work from board postings that match the type and tags of the data on the board. Board schemas are automatically generated from the wired up MagikBlocks.

The board interfaces are public so feel free to write your own spicy monsters.

## Configuration

Board configuration may be customized as part of the Done() function.

Currently configurable:
- Global tag matching sorting.
- Timeout and retry handling.
- Pull vs Push mode.

## Modes
Boards can operate in Pull Mode or Push Mode based on your configuration. Push is generally recommended. Both options support timeout and retry control.

### Push
In Push mode the board dispatches work to blocks as it gets it.
It keeps a promise that represents the work's completion and seamlessly starts the next task in the engine.

Important: Push pipelines do not short-circuit when the current value is already assignable to the requested final type. They continue selecting and executing forward-compatible steps (respecting tags/capabilities and registration order) until no such next step exists. Only then is the final value validated against the requested `TOut`.

### Pull
In Pull mode the orchestration loop repeatedly calls `GetWork<TIn>(request)`; the Board advances one step per call and completes with a typed output.

- Selection: Board picks the next block using Push scoring (`to:*` > capability overlap > registration order) against merged tags (board + request).
- Execute: Board runs that block with `request.Input`, emits `by:*`, updates tags, and calls `Complete<TOut>(output)` back to the orchestrator.
- No forward-only: Pull doesn’t enforce index monotonicity; a Roslyn analyzer ensures reachability.

