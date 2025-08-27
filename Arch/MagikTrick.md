# MagikTrick (Fenced Routing)

Use `.MagikTrick(...)` to create a “fork” that selects among multiple downstream candidates with the same input type.

How it works (current behavior):
- Trick is an `IMagikBlock<T,T>` that returns the input unchanged (identity) and emits intent tags you choose (e.g., `want:*`, `prefer:*`).
- Trick applies a one-hop selection fence internally so the very next selection can only pick from the Trick’s registered candidates.
- Within that fence, Trick computes the best candidate (capability overlap vs its emitted tags) and adds an explicit `to:<BlockTypeName>` so the intended candidate wins deterministically.
- Tags pass through; candidates can still emit additional tags that influence later steps. The fence is single-hop and does not leak further.

