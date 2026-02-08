# ImageGen Substrate

> **Status**: Draft  
> **Created**: 2026-02-05  
> **Parent**: [Spellcasting Branch](spellcasting-branch.md)

---

## Summary

Sub-branch of Spellcasting for AI image generation. Spells write efferent intent (`ImageGen`). Leaf daemons tail via `TailAsync`, generate images against their backend, write afferent fulfillment.

Initial leaf: OpenAI DALL-E. Additional providers (local models, other APIs) are future proposals that may extend the branch with provider-specific entry types.

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

The branch defines minimal, universal entry types. Provider-specific options (e.g., negative prompts, seeds, sampler settings) belong in leaf extension libraries, not the core branch.

### Efferent (Intent)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `ImageGen` | prompt | Generate image from text |

**Field details:**

```
ImageGen
  correlation-id: guid
  prompt: string              -- Text description of desired image
```

The core entry is intentionally minimal. Leaves that need additional parameters (size, quality, style) define their own extended entry types or configuration, following the pattern of other branch/leaf separations in Coven.

### Afferent (Fulfillment)

| Entry | Fields | Purpose |
|-------|--------|---------|
| `ImageGenerated` | correlationId, imageUri | Success |
| `ImageGenFault` | correlationId, faultKind, message | Failure |

**Field details:**

```
ImageGenerated
  correlation-id: guid
  image-uri: string           -- URI reference to generated image

ImageGenFault
  correlation-id: guid
  fault-kind: ContentPolicy | RateLimited | ModelUnavailable | InvalidPrompt | Unknown
  message: string
```

Images are referenced by URI, not embedded as bytes. This keeps journals lightweight and suitable for persistence/replay. The URI may point to:
- A temporary URL from the provider (e.g., OpenAI-hosted)
- A local file path after the leaf saves the image
- A blob store or cache location

How the leaf obtains and stores the image is a leaf concern.

---

## Leaves

The following pseudocode illustrates the daemon pattern. This is an abstract template showing the flow, not executable implementation.

```
DAEMON ImageGenDaemon (abstract template)
  tails: IScrivener<ImageGenEntry>
  
  ON ImageGen { correlation-id, prompt }:
    image-uri = generate-and-store(prompt)
    WRITE ImageGenerated { correlation-id, image-uri }
    
  ON error:
    WRITE ImageGenFault { correlation-id, fault-kind, message }
```

### OpenAI DALL-E Leaf

The initial implementation targets DALL-E 3 via the OpenAI API.

**Configuration:**

```csharp
record DalleImageGenConfig
{
    required string ApiKey { get; init; }
    string Model { get; init; } = "dall-e-3";
}
```

DALL-E-specific options (size, quality, style) are configured at the leaf level, not passed through the branch entry. This keeps the branch abstraction clean while allowing the leaf full access to provider capabilities.

---

## Build-Time Configuration

```csharp
coven.UseSpellcasting(spellcasting =>
{
    spellcasting.ImageGen.UseDalle(cfg =>
    {
        cfg.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        cfg.Model = "dall-e-3";
    });
});
```

---

## Scope

**In scope:**
- `ImageGenEntry` hierarchy with polymorphic serialization
- `ImageGenSpell` boundary type for Spellcasting
- `ImageGenerated` with URI reference
- `ImageGenFault` with fault kinds
- `DalleImageGenDaemon` leaf (OpenAI DALL-E)
- `DalleImageGenConfig` record
- Build-time configuration via `UseSpellcasting`

**Out of scope (future proposals):**
- Local model leaves (Stable Diffusion, etc.)
- Provider-specific branch extensions
- Image editing (inpainting, outpainting, variations)
- Image-to-image generation
- Multiple image generation (batch)
- Image storage/caching integration

---

## Checklist

- [ ] `ImageGenEntry` hierarchy with `[JsonPolymorphic]`
- [ ] `ImageGenSpell` with correlation-id, prompt
- [ ] `ImageGenerated` with image-uri
- [ ] `ImageGenFault` with fault kinds
- [ ] `DalleImageGenDaemon` extending `ContractDaemon`
- [ ] `DalleImageGenConfig` record
- [ ] `UseImageGen().UseDalle()` configuration
- [ ] Integration test: spell → daemon → result round-trip (mock backend)
- [ ] Toy: console app that generates image from prompt
