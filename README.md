# Coven

A tiny, composable **.NET 10** engine for orchestrating multiple agents to achieve big things.

## Highlights

* **Typed MagikBlocks**: implement `IMagikBlock<TIn,TOut>.DoMagik(...)` and compose work as pure(ish) functions.
* **Tag‑based routing**: a per‑ritual tag scope steers selection; blocks may also advertise **capabilities**.
* **DI‑first**: one builder on `IServiceCollection` (`BuildCoven`) with `MagikBlock<…>` and `LambdaBlock<…>` helpers; finish with `.Done(pull?: bool)`.
* **Journal Primitives**: reliable, distributable, and seamless to developers. Scriveners MUST support a long position.
* **Spellcasting (optional)**: minimal `ISpell<…>` interfaces + JSON‑schema generation for tool contracts.

---

## Quick Start

### 1) Hello, MagikBlocks (DI)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Coven.Core;
using Coven.Core.Builder;

sealed class BuildCodes : IMagikBlock<Empty, int[]>
{
    public Task<int[]> DoMagik(Empty _, CancellationToken ct = default)
    {
        int[] codes = new int[] {
            73, 102, 32, 111, 110, 108, 121, 32, 73, 32, 99, 111, 117, 108, 100, 32, 98, 101, 32, 115, 111, 32, 103, 114, 111, 115, 115, 108, 121, 32, 105, 110, 99, 97, 110, 100, 101, 115, 99, 101, 110, 116, 46
        };
        return Task.FromResult(codes);
    }
}

sealed class CodesToChars : IMagikBlock<int[], char[]>
{
    public Task<char[]> DoMagik(int[] codes, CancellationToken ct = default)
        => Task.FromResult(codes.Select(c => (char)c).ToArray());
}

sealed class JoinChars : IMagikBlock<char[], string>
{
    public Task<string> DoMagik(char[] chars, CancellationToken ct = default) => Task.FromResult(new string(chars));
}

var services = new ServiceCollection();
services.BuildCoven(b =>
{
    b.MagikBlock<Empty, int[], BuildCodes>();
    b.MagikBlock<int[], char[], CodesToChars>();
    b.MagikBlock<char[], string, JoinChars>();
    b.Done();
});

using var sp = services.BuildServiceProvider();

// Avoid GetRequiredService in production code (unless you know exactly what you are doing).
// Here we use it simply to keep the sample small and clear, but in production you should use a hosted service to run rituals.
var coven = sp.GetRequiredService<ICoven>();
var result = await coven.Ritual<string>();

Console.WriteLine(result); //If only I could be so grossly incandescent.
```

---

## Repository Layout

* **src/Coven.Core/** — runtime
* **src/Coven.Core.Tests/** — tests for core
* **src/Coven.Spellcasting/** — minimal spellcasting layer
* **src/Coven.Chat/** — chat primitives
* **architecture/** — flat architecture docs (see below)
* **build/** — CI/release scripts
* **INDEX.md**, **README.md**, **CONTRIBUTING.md**, **AGENTS.md**, license files in repo root

## Documentation

Start here:

* **Architecture Guide** → [`/architecture/README.md`](/architecture/README.md)
* **Core** → [`/architecture/Coven.Core.md`](/architecture/Coven.Core.md)
* **Spellcasting** → [`/architecture/Coven.Spellcasting.md`](/architecture/Coven.Spellcasting.md)
* **Chat** → [`/architecture/Coven.Chat.md`](/architecture/Coven.Chat.md)
* **Daemonology (hosts)** → [`/architecture/Coven.Daemonology.md`](/architecture/Coven.Daemonology.md)
* **Integrations (docs only)** → [`/architecture/Coven.Codex.md`](/architecture/Coven.Codex.md), [`/architecture/Coven.OpenAI.md`](/architecture/Coven.OpenAI.md), [`/architecture/Coven.Spellcasting.MCP.md`](/architecture/Coven.Spellcasting.MCP.md)

---

## Licensing

**Dual‑license (BUSL‑1.1 + Commercial):**

* **Community**: Business Source License 1.1 (BUSL‑1.1) with an Additional Use Grant permitting Production Use if you and your affiliates made **< US $100M** in combined gross revenue in the prior fiscal year. See `LICENSE`.
* **Commercial/Enterprise**: available under a separate agreement. See `COMMERCIAL-TERMS.md`.

*Change Date/License*: `LICENSE` specifies a Change License of **MIT** on **2029‑09‑11**.

## Support

* Patreon: [https://www.patreon.com/c/Goldenwitch](https://www.patreon.com/c/Goldenwitch)

> © 2025 Autumn Wyborny. BUSL 1.1, free for non-profits, individuals, and commercial business under 100m annual revenue.
