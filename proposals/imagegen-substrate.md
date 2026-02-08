# ImageGen Substrate

> **Status**: Draft  
> **Created**: 2026-02-05  
> **Parent**: [Spellcasting Branch](spellcasting-branch.md)

---

## Summary

Sub-branch of Spellcasting for AI image generation. Spells write efferent intent (`ImageGen`). Leaf daemons tail via `TailAsync`, generate images against their backend (local model or remote API), write afferent fulfillment.

Supports two leaf types from the start:
- **API leaf**: OpenAI DALL-E (and future API providers)
- **Local leaf**: Stable Diffusion via local inference

---

## Motivation

Image generation is a foundational capability for:
- Chat responses with visual content
- Data visualization (charts rendered as images)
- Creative agent workflows
- Report generation with embedded graphics

By modeling image generation as a Spellcasting substrate, agents gain image capabilities through the existing tool-call pattern without new integration work.

---

## Entries

Base: `ImageGenEntry : Entry`

### Efferent (Intent)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `ImageGen` | prompt, size?, style?, quality?, negativePrompt? | Generate image from text |

**Field details:**

```
ImageGen
  correlation-id: guid
  prompt: string              -- Text description of desired image
  negative-prompt: string?    -- What to avoid (local models)
  size: ImageSize?            -- Target dimensions
  style: ImageStyle?          -- Generation style hint
  quality: ImageQuality?      -- Quality/speed tradeoff
  seed: int?                  -- Reproducibility (local models)
```

**Enums:**

```
ImageSize = Square1024 | Landscape | Portrait | Square512
  -- Leaves map to backend-specific sizes
  -- Square1024 → DALL-E "1024x1024", SD "1024x1024"
  -- Landscape  → DALL-E "1792x1024", SD "1216x832"
  -- Portrait   → DALL-E "1024x1792", SD "832x1216"
  -- Square512  → DALL-E n/a (fallback), SD "512x512"

ImageStyle = Vivid | Natural
  -- DALL-E: maps directly
  -- Local: influences sampler/cfg settings

ImageQuality = Standard | High
  -- DALL-E: "standard" vs "hd"
  -- Local: step count / model selection
```

### Afferent (Fulfillment)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `ImageGenerated` | correlationId, content, contentType, revisedPrompt? | Success |
| `ImageGenFault` | correlationId, faultKind, message | Failure |

**Field details:**

```
ImageGenerated
  correlation-id: guid
  content: ImageContent       -- The image data
  revised-prompt: string?     -- DALL-E may revise prompt for safety
  generation-ms: int?         -- Time taken
  seed-used: int?             -- For reproducibility (local)

ImageContent = Url { url, expires? } | Bytes { data, format }
  -- Url: temporary hosted URL (API providers)
  -- Bytes: raw image data (local generation, or fetched from URL)

ImageGenFault
  correlation-id: guid
  fault-kind: ContentPolicy | RateLimited | ModelUnavailable | InvalidPrompt | Unknown
  message: string
```

---

## Leaves

Each leaf extends `ContractDaemon`, tails `IScrivener<ImageGenEntry>`, processes `ImageGen` entries, writes fulfillment:

```
DAEMON ImageGenDaemon (abstract pattern)
  tails: IScrivener<ImageGenEntry>
  
  ON ImageGen { correlation-id, prompt, ... }:
    image = generate(prompt, options)
    WRITE ImageGenerated { correlation-id, content: image }
    
  ON error:
    WRITE ImageGenFault { correlation-id, fault-kind, message }
```

### OpenAI DALL-E Leaf

```
DAEMON DalleImageGenDaemon
  config: DalleImageGenConfig { apiKey, model, defaultSize, defaultQuality }
  
  ON ImageGen:
    response = await openai.Images.Generate(
      prompt: entry.Prompt,
      model: config.Model,           -- "dall-e-3"
      size: map(entry.Size),
      style: map(entry.Style),
      quality: map(entry.Quality)
    )
    WRITE ImageGenerated {
      correlation-id,
      content: Url { response.Url },
      revised-prompt: response.RevisedPrompt
    }
```

**Configuration:**

```csharp
record DalleImageGenConfig
{
    required string ApiKey { get; init; }
    string Model { get; init; } = "dall-e-3";
    ImageSize DefaultSize { get; init; } = ImageSize.Square1024;
    ImageQuality DefaultQuality { get; init; } = ImageQuality.Standard;
    bool FetchBytes { get; init; } = false;  // Convert URL to bytes
}
```

### Local Stable Diffusion Leaf

```
DAEMON LocalImageGenDaemon
  config: LocalImageGenConfig { endpoint, model, defaultSteps }
  
  ON ImageGen:
    response = await http.Post(config.Endpoint + "/txt2img", {
      prompt: entry.Prompt,
      negative_prompt: entry.NegativePrompt,
      width: map(entry.Size).Width,
      height: map(entry.Size).Height,
      steps: qualityToSteps(entry.Quality),
      seed: entry.Seed ?? -1
    })
    WRITE ImageGenerated {
      correlation-id,
      content: Bytes { response.Images[0], "png" },
      seed-used: response.Seed
    }
```

**Configuration:**

```csharp
record LocalImageGenConfig
{
    required string Endpoint { get; init; }  // e.g., "http://localhost:7860"
    string? Model { get; init; }             // Checkpoint to load
    int DefaultSteps { get; init; } = 30;
    int DefaultCfgScale { get; init; } = 7;
    string DefaultSampler { get; init; } = "DPM++ 2M Karras";
}
```

**Backend compatibility:** Targets Automatic1111/Forge WebUI API. Other local backends (ComfyUI, InvokeAI) would be additional leaves.

---

## Build-Time Configuration

```csharp
coven.UseSpellcasting(spellcasting =>
{
    // Option A: OpenAI DALL-E
    spellcasting.ImageGen.UseDalle(cfg =>
    {
        cfg.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        cfg.Model = "dall-e-3";
    });
    
    // Option B: Local Stable Diffusion
    spellcasting.ImageGen.UseLocal(cfg =>
    {
        cfg.Endpoint = "http://localhost:7860";
    });
    
    // Option C: Both (first configured is default, or use hints)
    spellcasting.ImageGen.UseDalle(...);
    spellcasting.ImageGen.UseLocal(...);
});
```

When multiple leaves are configured, dispatch could use:
- First-registered wins (simple)
- Capability hints in spell (future: `preferLocal: true`)
- Load balancing / fallback (future)

For v1: single leaf active, last-configured wins.

---

## Substrate Manifest

```
SUBSTRATE-MANIFEST ImageGen
  spell-types: [ImageGenSpell]
  inner-journal: ImageGenEntry
  daemons: [DalleImageGenDaemon | LocalImageGenDaemon]
  
  transforms:
    ImageGenSpell → ImageGen
  
  result-transforms:
    ImageGenerated → SpellResult
    ImageGenFault → SpellFault
```

---

## Scope

**In scope:**
- `ImageGenEntry` hierarchy with polymorphic serialization
- `ImageGenSpell` boundary type for Spellcasting
- `DalleImageGenDaemon` leaf (OpenAI DALL-E)
- `LocalImageGenDaemon` leaf (Automatic1111/Forge API)
- `ImageContent` union (URL vs bytes)
- Build-time configuration via `UseSpellcasting`
- Metagraph capability publishing

**Out of scope (future proposals):**
- Image editing (inpainting, outpainting, variations)
- Image-to-image generation
- Multiple image generation (batch)
- Streaming progressive rendering
- Additional local backends (ComfyUI, InvokeAI)
- Image storage/caching substrate integration

---

## Checklist

- [ ] `ImageGenEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `ImageGenSpell` with correlation-id, prompt, options
- [ ] `ImageGenerated` with `ImageContent` (URL | Bytes)
- [ ] `ImageGenFault` with fault kinds
- [ ] `DalleImageGenDaemon` extending `ContractDaemon`
- [ ] `DalleImageGenConfig` record
- [ ] `LocalImageGenDaemon` for Automatic1111/Forge
- [ ] `LocalImageGenConfig` record
- [ ] `UseImageGen().UseDalle()` / `UseLocal()` configuration
- [ ] Size/style/quality enum mapping per backend
- [ ] Metagraph capability registration
- [ ] Integration test: spell → daemon → result round-trip (mock backend)
- [ ] Toy: console app that generates image from prompt
