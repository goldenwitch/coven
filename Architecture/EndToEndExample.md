# End-to-End Builder Example

Although `ICoven` exposes a simple API `Ritual<TIn, TOut>(TIn input)`, what happens in between is flexible and decided at runtime by tags and capabilities. Here’s a minimal end‑to‑end workflow that routes across multiple blocks to produce a final result.

```
using System.Threading.Tasks;
using Coven.Core;
using Coven.Core.Builder;
using Coven.Core.Tags;

// A tiny document type passed between blocks
public sealed class Doc { public string Text { get; init; } = string.Empty; }

// Blocks
public sealed class ParseAndTag : IMagikBlock<string, Doc>
{
    public Task<Doc> DoMagik(string input)
    {
        if (input.Contains("!")) Tag.Add("exclaim");
        Tag.Add("style:loud");
        return Task.FromResult(new Doc { Text = input });
    }
}

public sealed class AddSalutation : IMagikBlock<Doc, Doc>, ITagCapabilities
{
    // This block is most capable when the input is marked with `exclaim`
    public IReadOnlyCollection<string> SupportedTags => new[] { "exclaim" };
    public Task<Doc> DoMagik(Doc input)
        => Task.FromResult(new Doc { Text = $"☀ PRAISE THE SUN! ☀ {input.Text}" });
}

public sealed class UppercaseText : IMagikBlock<Doc, Doc>
{
    public Task<Doc> DoMagik(Doc input)
        => Task.FromResult(new Doc { Text = input.Text.ToUpperInvariant() });
}

public sealed class ToOut : IMagikBlock<Doc, string>
{
    public Task<string> DoMagik(Doc input) => Task.FromResult(input.Text);
}

// Build a Coven: string -> string
var coven = new MagikBuilder<string, string>()
    .MagikBlock(new ParseAndTag())                              // string -> Doc (emits tags)
    .MagikBlock<Doc, Doc>(new AddSalutation())                  // Doc -> Doc (capability: exclaim)
    .MagikBlock<Doc, Doc>(new UppercaseText(), new[] { "style:loud" }) // Doc -> Doc (builder-assigned capability)
    .MagikBlock<Doc, string>(new ToOut())                       // Doc -> string (finish)
    .Done();

var result = await coven.Ritual<string, string>("hello coven!!!");
// => "☀ PRAISE THE SUN! ☀ HELLO COVEN!!!"
```

Key points:
- `ICoven.Ritual<TIn, TOut>` only fixes the start and end types; the internal route is chosen dynamically per step based on explicit `to:*` tags (optional), capability overlap, then registration order.
- Tags are scoped to a single invocation of `PostWork`/`Ritual`; blocks use `Tag.Add(...)` and `Tag.Contains(...)` during execution only.
- Capabilities can be advertised by a block (`ITagCapabilities`) and/or assigned at registration time with builder overloads.
