# Coven.Core.Debug — Scrivener Taps

Minimal, zero‑ceremony way to observe journal writes without changing any branch/leaf code: wrap an existing `IScrivener<T>` with a tiny decorator that calls an observer delegate after each write.

## Goal
- Add an observer delegate to an existing scrivener.
- Do not change `IScrivener<T>`.
- Do not write back to the observed journal.
- Preserve normal tail/read behavior and positions.

## Concept
- `TappedScrivener<TEntry>` has an inner `IScrivener<TEntry>`.
- On `WriteAsync(entry)`, it awaits the inner write to get the assigned `position`, then invokes an observer delegate: `Action<long, TEntry>`.
- All read APIs (`TailAsync`, `ReadBackwardAsync`, `WaitForAsync`) simply delegate to the inner scrivener.

## Behavior
- Observation happens after the inner write completes, using the actual assigned position.
- Observer exceptions are ignored (best‑effort, non‑interfering).
- No writes back to the observed journal; tap is read‑only aside from the delegated call.
- Overhead is a single delegate invocation per write.

## Notes
- Keep observers cheap; they run inline after writes. If you need heavy work, queue it yourself inside the delegate.
- Because it wraps a specific IScrivener<T> instance, it only observes entries written through that instance.
- Because we need to register the TappedScrivener as the final implementation for IScrivener<TEntry> we will need some way to disambiguate them.
