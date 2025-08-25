# Coven

Coven is an engine for orchestrating multiple AI coding agents to collaborate on complex tasks.

Coven coordinates agents to operate in a shared context with clear roles, policies, and handoffs. It builds on Codex CLI’s strong sandboxing and tooling, adding multi‑agent workflows, and traditional compute for coordination. Technically, you could wrap any tool with it, but we are targeting a mix of LLMs and traditional programming.

# Project Scope
- Run on one box. A distributed version of this might be offered in the future, but for now just make calls out to the network in a MagikBlock if you need to do remote stuff. Send me money if you want the distributed stuff.
- A minimal set of samples that satisfy the needs of the selfish developer writing these docs while reluctantly avoiding avoiding work.
- A library of extensions to the core orchestration engine that makes it easy to orchestrate AI Agents.
- Initially, just dotnet. Something something something money something something something typescript.

# MagikBlock
A MagikBlock is our core logic unit. We compose MagikBlocks into trees

## Functional building
A MagikBuilder offers type-safe methods for constructing a Coven orchestration engine. This guarantees that every MagikBlock will only provide or be provided things it understands how to operate on.

After a MagikBuilder is finalized, the Coven engine is locked for changes (immutable). This ensures that there are no run-time surprises due to type mismatch.

### Builder Example
There are three seperate ways to add a MagikBlock based on how you want to initialize it.

MagikBuilder
- .MagikBlock(builder => return m)
- .MagikBlock(new MagikBlock<T, T2>())
- .MagikBlock(T => { logic return t2 })
- .Done()

Each .MagikBlock returns a MagikBlockRegistration(T) object that itself transparently uses the MagikBuilder it is instantiated by.

### Overriding auto-tagging.
By default the builder adds a tag to the outgoing messages for each MagikBlock that represents the downstream worker. That way each worker is automatically looking for work assigned to it. If you need to disable this for whatever reason .MagikBlock has an optional argument to specify a lambda that outputs a list of Tags with absolute control. This is in addition to any tags that the MagikBlock itself outputs.

The function that assigns tags MUST be static and only has access to limited information. Sure, this is a painful limitation for those of you out there with a giant brain but at least I'm not saying the word YAML.

> As one more spicy caveat: Overriding tagging in this way can result in cycling. That's on you buddy; I warned you.

## Capabilities and Tagging (Current Model)
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

This enables concise, per-step dynamic routing.

### Magic Tags Summary (current)

- `to:#<index>`: Force the next block by registry index; overrides everything else.
- `to:<BlockTypeName>`: Force the next block by type name; overrides everything else.
- `by:<BlockTypeName>`: Emitted by the Board after each step for tracing only; does not affect selection.
- `prefer:<name>`: A persistent “soft” preference considered alongside current-step tags during capability scoring.

> We do not plan to add more magical tags. Where possible we will remove magic in favor of explicit APIs and capability routing.

## End-to-End Builder Example

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

// Execute the workflow — routing is decided step-by-step:
// 1) ParseAndTag runs and emits tags
// 2) Router prefers blocks whose capabilities match current tags
// 3) Ties are broken by registration order
var result = await coven.Ritual<string, string>("hello coven!!!");
// => "☀ PRAISE THE SUN! ☀ HELLO COVEN!!!"
```

Key points:
- `ICoven.Ritual<TIn, TOut>` only fixes the start and end types; the internal route is chosen dynamically per step based on explicit `to:*` tags (optional), capability overlap, then registration order.
- Tags are scoped to a single invocation of `PostWork`/`Ritual`; blocks use `Tag.Add(...)` and `Tag.Contains(...)` during execution only.
- Capabilities can be advertised by a block (`ITagCapabilities`) and/or assigned at registration time with builder overloads.

## Split between handlers
Use `.MagikTrick(...)` to create a “fork” that selects among multiple downstream candidates with the same input type.

How it works (current behavior):
- Trick is an `IMagikBlock<T,T>` that returns the input unchanged (identity) and emits intent tags you choose (e.g., `want:*`, `prefer:*`).
- Trick applies a one-hop selection fence internally so the very next selection can only pick from the Trick’s registered candidates.
- Within that fence, Trick computes the best candidate (capability overlap vs its emitted tags) and adds an explicit `to:<BlockTypeName>` so the intended candidate wins deterministically.
- Tags pass through; candidates can still emit additional tags that influence later steps. The fence is single-hop and does not leak further.

## Type limitations
Each MagikBlock supports generic TOutput and T, T2 ... T20 inputs.
- To handle arbitrary numbers of types of inputs, we dynamically generate T2 -> T20 based on usage.

I also have helped you out by adding a Roslyn analyzer that enforces that all MagikBlocks have only immutable records as input. You are welcome.

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
Boards can operate in Pull Mode or Push Mode based on your configuration. I very strongly recommend Push. Unless you are using this framework to write something crazy, Push will do everything you need and faster. Both board options automatically support timeout and retry control. Don't write your own in blocks, I promise life will be better.

### Push
In Push mode the board dispatches work to blocks as it gets it.
It keeps a promise that represents the works completion and seamlessly starts the next task in the engine.

### Pull
In Pull mode the board waits until an operator reaches out with a request.

The request must contain:
- The MagikBlock that is trying to execute.
- Which tags it says it wants.

After a request hits the box the board finds a posting that exactly matches the tags and magikblock. In the event of two postings that match the board will use the posting that came first. You can override this logic when the board gets registered.

Answering when a Pull lands on the board requires a lock on an index that the board uses internally to decide how to answer. This lock happens for EVERY lookup. Yes, this is totally something that can be fixed. I'll fix it if people send me enough money.

# How to write fancy AI stuff.
We offer a secondary library (Coven.Spellcasting) with some built in constructs you might need for coordinating LLMs.

Create MagikUsers (representing specialized agents) and then inform them with books they can read to understand:
1. Which tools they can call (Spellbooks).
2. What tests they can run to validate which tasks (Testbooks)
3. What information and role they should assume as well as any other context. (Guidebooks)

## MagikUser
Rather than hand-rolling your calls to LLMs you can use the MagikUser template as a base to add:
1. Configuration
2. Built in wrapper for codex CLI
3. Tool calls
4. Agent definitions
5. Test and validation integration

## Spellbook
A spellbook contains a list of tools that the agent has access to.
We automatically add core AI coordination constructs like "I'm done" and "I think X should work on this instead of me."

Additionally, in our samples we demonstrate wrapping an MCP servers tools and making them available via a spellbook.

## Testbook
A testbook contains a list of evaluations to run. These validations include a mapping representing when and where the agent should consider leveraging them.

Our samples show how to build a testbook dynamically based on what the agent changes in the codebase using git deltas and code coverage tests.

## Guidebook
A guidebook describes:
1. Who the agent is
2. What the agent can do
3. Any other information that is helpful every time the agent runs.
4. Any information that is helpful at the start of the conversation with the agent.

Our samples demonstrate how to build a guidebook that has all of the code in your codebase pre-loaded into context.

# Connectivity
So, once you've got a lovely console app where you can talk to your agents and get them to do whatever you want, what's the next step?

Making it so you can talk to them wherever they are of course.

We offer an adapter interface that maps an arbitrary message platform (say for example Discord) into your Coven for processing and response.

In our samples we include two demonstrations of how to wire this up.
1. Console
2. Discord

# The boring stuff
It couldn't all be fun could it?

## Contributing
Feel free to fork and open PRs. If you are adding a new feature please open an issue first. Starting with a problem makes solutions smell better.

If someone has multiple contributions they would like to make to this repo reach out to me directly and I'll do my best to clear out any friction.

### Contribution rules
- Compilation time validation is not optional. If you really want to add a clever feature use Roslyn to make it so we still get compilation time validation.
- We could enable users to define a configuration file somewhere that turns into everything we have here. See above rule.
- We use a PR gate to validate test coverage, test passing, and test run time. Keep tests deterministic and fast running.
- I have linting preferences. Sorry.
- If you add new features to the core library, please make sure they are represented in new samples.
- Dependency injection and configuration must happen at the very root of integration.

Breaking these rules is fine, we can talk about it in the PR if you've got a good reason.

## Licensing
We have an explicit dual-licensing scheme.

### Eligibility for MIT License
Individuals, non-profits, and companies with **annual revenue under USD $100 million** may use this project under the terms of the [MIT License](./LICENSE).

### Commercial License Requirement
Any company, organization, or entity with **annual revenue equal to or greater than USD $100 million** must obtain a commercial license to use this software in any form (including use, modification, distribution, SaaS, or incorporation into products).

To obtain a commercial license, please contact:

Autumn Wyborny
- Email: autumnwyborny@witchfolio.com
- Github: goldenwitch
- LinkedIn (Must be desperate): https://www.linkedin.com/in/autumn-wyborny/

> I promise I don't bite (even if you have tough compliance constraints).

## Support the project

[![Support on Patreon](https://img.shields.io/badge/Support-Patreon-e85b46?logo=patreon)](https://www.patreon.com/c/Goldenwitch)
