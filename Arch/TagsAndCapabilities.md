# Capabilities and Tagging (Current Model)

- Tags are scoped to a single Board request: the Board creates a per-request tag scope and the static `Tag` helper points to it for the duration of `PostWork`.
- Blocks can emit tags during execution: `Tag.Add("fast")`, check them: `Tag.Contains("exclaim")`, or rely on tags provided at the start of `PostWork`.
- Blocks can advertise capability tags via `ITagCapabilities.SupportedTags`, and you can also assign capabilities at registration time via builder overloads:

```
ICoven coven = new MagikBuilder<string, double>()
    .MagikBlock((string s) => Task.FromResult(s.Length))
    .MagikBlock<int, double>(new IntToDoubleA(), new[] { "fast" }) // builder capability
    .MagikBlock<int, double>(new IntToDoubleB())                    // fallback
    .Done();
```

- Routing order per step:
  1) If explicit `to:*` tags are present (`to:#<index>` or `to:<BlockTypeName>`), they override selection (still must accept current type and be forward in registration order).
  2) Otherwise choose the candidate with the highest overlap between the current TagSet and the candidate's capabilities.
  3) Break ties by registration order.

Auto forward preference
- Each registered block automatically advertises a soft capability `next:<BlockTypeName>`.
- After a step runs, the Board adds `next:<DownstreamBlockTypeName>` for all reachable downstream blocks (by type) to bias selection forward by default. This is a preference, not a `to:*` override; more capable candidates can still win.

### Magic Tags Summary (current)

- `to:#<index>`: Force the next block by registry index; overrides everything else.
- `to:<BlockTypeName>`: Force the next block by type name; overrides everything else.
- `by:<BlockTypeName>`: Emitted by the Board after each step for tracing only; does not affect selection.
- `prefer:<name>`: A persistent “soft” preference considered alongside current-step tags during capability scoring.

> We do not plan to add more magical tags. Where possible we will remove magic in favor of explicit APIs and capability routing.

