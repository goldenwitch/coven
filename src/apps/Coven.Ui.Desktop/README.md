# Coven.Ui.Desktop

Cross-platform desktop client for Coven, built on Avalonia. Chat with an agent — hosted or running locally on your own machine; responses stream token by token and reasoning lands in its own pane.

## Prerequisites

- .NET 10 SDK
- An API key for Anthropic, OpenAI, or Gemini — or, for the local provider, a GGUF model file (the app can download one for you)

## Run

```pwsh
dotnet run --project src/apps/Coven.Ui.Desktop
```

Without a key or a local model the window still opens and points at **Options**.

## Options

Click **Options** to choose a provider, enter its API key, and pick a model. Each provider keeps its own key and model, so switching back and forth does not discard credentials.

**Refresh list** queries the provider's models endpoint live, so a newly released model is selectable the day it ships — nothing here is a hardcoded list. The **Model id** field is what actually gets used; the dropdown fills it in. It stays editable so a failed fetch — no key yet, offline, provider outage — never blocks you from setting a model by hand.

Models are classified by family pattern rather than exact id, so an unreleased `claude-sonnet-9` or `gpt-9` is grouped and labelled correctly without a code change. Anything unrecognized appears under `other` rather than being hidden.

The seed model used before the first catalog fetch is only a starting point — keep it current. Models are retired on a published schedule, and a seed that has passed its retirement date turns first run into a 404.

Selecting **Local** swaps the API-key field for a models directory and a **Browse models…** button. There is no key to enter, and the catalog is a scan of that directory rather than an HTTP call.

### Billing

The Anthropic API bills against **API credits**, which are a separate pool from a claude.ai Pro/Max subscription. A subscription grants no API credits; top up at [console.anthropic.com](https://console.anthropic.com) → Plans &amp; Billing. A depleted balance surfaces as an HTTP 400 from the gateway and appears in the transcript as a session failure.

### What resets the conversation

Journal hydration is not implemented yet, so rebuilding the session discards the transcript. The options window warns before you save.

| Change | Effect |
|--------|--------|
| Model, on Anthropic | Applied in place; conversation continues |
| Model, on OpenAI, Gemini, or Local | Rebuild; conversation cleared |
| API key, provider, or system prompt | Rebuild; conversation cleared |

The asymmetry is real, not an oversight: `ClaudeClientConfig` exposes settable properties and its gateway reads `Model` when building each request, while `OpenAIClientConfig`, `GeminiClientConfig`, and `LLamaSharpClientConfig` are records with `init` properties that cannot change after registration. For the local provider it could not be a hot swap anyway — changing models means unloading several gigabytes of weights and loading several more.

## Configuration

Settings live at `%APPDATA%\Coven\settings.json` (`~/.config/Coven/settings.json` on Unix).

API keys are encrypted with DPAPI for the current Windows account. .NET exposes no cross-platform equivalent, so on other platforms they are stored in plain text with the file restricted to the owner — the options window says so plainly rather than implying protection that is not there.

Environment variables seed any field not already set, matching the toys and samples:

| Variable | Description |
|----------|-------------|
| `ANTHROPIC_API_KEY` | Anthropic API key |
| `OPENAI_API_KEY` | OpenAI API key |
| `GEMINI_API_KEY` / `GOOGLE_API_KEY` | Gemini API key |
| `CLAUDE_MODEL` | Overrides the stored Anthropic model |
| `OPENAI_MODEL` | Overrides the stored OpenAI model |
| `GEMINI_MODEL` | Overrides the stored Gemini model |
| `HF_TOKEN` / `HUGGING_FACE_HUB_TOKEN` | Hugging Face access token |
| `COVEN_MODELS_DIR` | Directory scanned for local GGUF models |
| `COVEN_SYSTEM_PROMPT` | System prompt for the session |

## Providers

Anthropic, OpenAI, Google Gemini, and **Local** (a GGUF model run in-process through LLamaSharp). Each keeps its own key and model.

Gemini is the only one whose models endpoint reports capability directly — `supportedGenerationMethods` says whether a model can hold a conversation, so its chat filtering is authoritative rather than a family-prefix heuristic, and its context windows come from `inputTokenLimit` rather than inference. It reports no creation timestamp, so its list is ordered by identifier descending as a stand-in for newest-first.

Only the hosted providers emit reasoning. A local GGUF has no separate reasoning channel, so its branch declares no `AgentThought` and the covenant omits the route that feeds the reasoning pane. Declaring it anyway is a covenant validation error, not an empty pane.

## Local Models

Select **Local** in Options and either point at a `.gguf` file you already have or download one in-app.

### Backend selection

The app references both the CPU and CUDA 12 LLamaSharp backends and picks between them at runtime. CUDA is preferred with auto-fallback enabled, so a machine without a usable GPU gets the CPU backend rather than a hard failure. **Probe backend** in Options reports which one would load — worth checking, because the difference between CUDA and CPU is the difference between a usable local model and one that looks hung.

Selection happens the first time a local session is built, not at startup: probing touches GPU drivers, and someone who only ever talks to a hosted provider should not pay for it. LLamaSharp freezes its configuration once the native library loads, so nothing else in the app may call into it first.

All layers are offloaded to the GPU (`GpuLayerCount = -1`). The CPU backend ignores it. The context is 8192 tokens rather than the library's 2048 default, which is only a few exchanges on models routinely trained for 32K; the KV cache is charged against the same memory budget as the weights, so it is not raised further by default.

## Running it

`Coven.cmd`, in the repository root, launches the app. It resolves every path from its own
location, so it works from any clone directory and any working directory; it prefers a Release
build, falls back to Debug, and builds Release only if neither exists.

Keep it CRLF and ASCII if you edit it. `cmd.exe` drops the first character of every line in an
LF-only batch file, which fails in a way that looks nothing like a line-ending problem — the
`.gitattributes` rule exists to stop a clone reintroducing that.

A `.cmd` always shows Explorer's generic script icon, since a batch file cannot carry one of
its own. For a shortcut with the application icon on it, run once:

```
powershell -ExecutionPolicy Bypass -File build\New-CovenShortcut.ps1
```

That writes `Coven.lnk` next to the launcher; `-Desktop` and `-StartMenu` place copies there
too. Shortcuts are not committed and cannot be — a `.lnk` stores absolute paths, so one is
only ever valid on the machine that made it, which is why this is a script rather than a file
in the repository.

## Look

The palette is a DMC embroidery floss card, so every colour in the interface can be bought as
thread. DMC 792 is the primary, 3826 the secondary, 801 the accent, and the page is 3866 — an
off-white rather than white. The remaining threads carry the quieter states: 422 for rules,
167 for secondary text, 3755 for selection and hover. The numbers are kept in the resource
names in `Theme/Palette.axaml` for that reason.

Surfaces are materials rather than fills, and each one means something: pressed paper for
anything holding content, construction paper for the accent, stamped foil for the secondary
action. A user's turn and an application notice are told apart by material, not by tint, so
the difference survives a glance.

The paper and construction textures are generated at startup in `Theme/PaperTexture.cs`
instead of being shipped as images — a fibre grain is cheap to draw and expensive to store,
and generating it makes the tint a parameter, so the same fibre structure can be dyed to any
thread on the card. Every tile wraps at its edges, which is what keeps a full window from
showing a grid. Foil is the exception: its sheen is a gradient, because a highlight has to
track the shape it is on rather than repeat across it, and only its rolled grain is a texture.

### Why a model may refuse to load

llama.cpp explains its own failures precisely — an unrecognised architecture, a missing tensor, an allocation that will not fit — but only on its native log, which a `WinExe` discards. Those messages are routed into the application log, and the last error is attached to what the user is shown. Without it every failure reads `Failed to load model '<path>'`, which names the file and nothing about why.

The most common cause is a model newer than the bundled runtime. A GGUF records its architecture, and llama.cpp only loads architectures it knows; the app pins **LLamaSharp 0.27.0**. Upgrading the pin is what adds support for a newer family.

Some models also need a runtime feature rather than just an architecture. Multi-token-prediction (`-MTP-`) builds carry an extra block that plain llama.cpp cannot load — it fails on a missing tensor in the final block — and want a purpose-built runtime with `--spec-type draft-mtp`. Prefer the non-MTP publication of the same model.

### Hugging Face browser

**Browse models…** searches Hugging Face for repositories tagged as containing GGUF weights, most-downloaded first, then lists the `.gguf` files in the one you pick — smallest first, because quantization size is the axis you are actually choosing on. A repository commonly holds the same model at six quantizations from 3 GB to 30 GB.

Downloads stream straight to disk with live progress and a transfer rate, and can be cancelled. A cancelled or interrupted transfer leaves a `.partial` sidecar and resumes with a range request on the next attempt; the real filename appears only once the file is complete, so a half-downloaded model can never be picked up by the directory scan and loaded.

Gated repositories need an access token — accept the repo's terms on huggingface.co, then paste a token into Options. A 401 or 403 says so rather than reporting a generic failure.

### What the browser tells you

Selecting a repository fills a details pane:

- **What the model is** — the first prose paragraph of the model card, preferring a `Description` or `Introduction` section over the build and quantization asides that often precede it. Links and HTML are reduced to plain text and fenced code is skipped, so the summary is sentences rather than `apt-get` lines.
- **Specifications** — architecture, parameter count and context length, read from the GGUF metadata itself rather than guessed from the filename, plus the license and descriptive tags.
- **What it takes to run** — an estimated memory requirement and a tier badge, from `Low-spec friendly` through `Workstation`, each with concrete guidance ("wants a 24 GB GPU such as an RTX 3090 or 4090"). Bits-per-weight is shown when the parameter count is known: it is the most direct measure of how much quality a quantization traded for size.
- **Whether it is complete** — stated outright as either a single file or a set of parts that download together.

The memory figure is the weights plus working space, and it is an estimate. Real use rises with context length, since the KV cache grows with it, and falls when layers are offloaded to the GPU. It answers "will this plausibly run here?", not "how much will this allocate".

Vision projectors (`mmproj-*.gguf`) are filtered out. They are real GGUF files and the smallest in a multimodal repository — so they would head a size-ordered list — but they are adapters, not models you can talk to.

### Multi-part models

Large models are published split across numbered files (`…-00001-of-00003.gguf`). The parts are not alternatives to each other: llama.cpp is given the **first** part and reads the rest from alongside it, so every part must be present and only the first is loadable.

The browser therefore lists a split model **once**, sized as the whole set and tagged `multi-part`, and downloads every part when you select it. Listing the parts separately is a trap — ordered by size, the small tail part looks like the cheap quantization to try first, and downloading it alone yields a multi-gigabyte file that can never load.

The same rule applies on disk: the local catalog offers only first parts, shows the combined size, and marks a set `INCOMPLETE` when parts are missing rather than letting it fail at load time with an error about tensors.

### Model size

Nothing prevents you from selecting a model too large for your hardware — a 27B at BF16 is roughly 54 GB and needs to fit in VRAM (or spill to system RAM and crawl). The listed size is the number to check; quantized builds such as `Q4_K_M` are usually the practical choice.

Sizes are reported from the LFS object rather than the tree entry: weights are stored via LFS, where the top-level size is the pointer file and is misleadingly tiny.

## How It Works

The ritual is the application's lifetime. `Program` builds the host, starts `Ritual<Empty, Empty>` on a background task, and then runs the UI. `UiHostBlock` runs inside the ritual scope, publishes the scope-resident shell journal to `SessionContext`, and holds the ritual open until shutdown — daemons are started by the scope and would stop if the block returned.

Journals are scoped and `CovenExecutionScope.CurrentProvider` is internal, so block injection is the only supported route to them.

```
UiChannel ──▶ ChatAfferent ──▶ AgentPrompt ──▶ Claude
                                                 │
     ┌───────────────────────────────────────────┤
     ▼                                           ▼
AgentAfferentChunk ──▶ ChatChunk           AgentResponse ──▶ ChatEfferent
     │                                                            │
     └────────────────▶ UiChannel ◀───────────────────────────────┘

AgentThought ──▶ UiThought  (shell journal, reasoning pane)
```

Two journals: `ChatEntry` carries the conversation, `UiEntry` carries reasoning and notices. A `BranchManifest` holds exactly one journal entry type, and reasoning does not belong in the transcript.

## Streaming

Chunks render as they arrive; the windowed `ChatEfferent` then replaces the accumulated text as authoritative. Chunk appends are coalesced onto a single dispatcher post — one post per token starves the UI thread on long responses.

## Status

M1 plus model and provider switching: chat, streaming, reasoning pane, options window, live model catalogs for Anthropic, OpenAI, and Gemini, local GGUF inference, and the Hugging Face model browser.

Not yet built: journal hydration (so a rebuild keeps the conversation), the journal inspector, and live window-policy tuning.

## See Also

- Leaf: `Coven.Chat.Ui`
- Shell journal: `Coven.Ui.Shell`
- Tests: `src/Coven.E2E.Tests/Ui/UiChatTests.cs`, `src/Coven.E2E.Tests/Models/LocalModelTests.cs`
