# Pull Mode (Design Pin)

Pull mode advances work step-by-step under an orchestrator while preserving the simple `Ritual<TIn, TOut>` API.

- Selection: Reuses push-mode scoring (`to:*` > capability overlap > registration order); no forward-only constraint.
- Execution: Each step runs under a tag scope. After running, the Board adds `by:<BlockTypeName>` and persists tags for the next step.
- Strongly-typed sink: The Board compiles a per-block pull wrapper that executes `DoMagik` and then calls `FinalizePullStep<TOut>` which invokes `IOrchestratorSink.Complete<TOut>(...)` with a strict generic `TOut` (declared block output type), avoiding per-call reflection.
- Finality: The orchestrator completes when the step’s `TOut` is assignable to the requested final type; otherwise it issues the next `GetWork` call.
