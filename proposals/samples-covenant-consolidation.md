# Consolidate Samples to Declarative Covenant Pattern

> **Status**: Proposal  
> **Created**: 2026-01-24  
> **Depends on**: [declarative-covenants.md](declarative-covenants.md)

---

## Problem

The samples directory currently contains two samples demonstrating redundant patterns:

| Sample | Pattern | Files |
|--------|---------|-------|
| `01.DiscordAgent` | Imperative RouterBlock | Program.cs, RouterBlock.cs, DiscordOpenAITemplatingTransmuter.cs |
| `02.DeclarativeDiscordAgent` | Declarative Covenants | Program.cs |

This creates several issues:

1. **Maintenance burden** — Two samples doing the same thing means double the updates when APIs change.

2. **Confusing onboarding** — New users see two approaches and must decide which is "correct." The numbering implies 01 is foundational and 02 is advanced, but actually 02 is the recommended path.

3. **Implicit deprecation** — The RouterBlock pattern in 01 is effectively superseded by covenants, but we maintain it as if it's a viable alternative.

4. **Extra code for parity** — Sample 01 requires `RouterBlock.cs` (~60 lines) that exists purely to demonstrate an obsolete pattern.

---

## Proposal

1. **Update Sample 01 to use declarative covenants** — Port the covenant pattern from 02 into 01, eliminating `RouterBlock.cs`.

2. **Delete Sample 02** — It exists only to contrast with 01. Once 01 uses covenants, 02 is redundant.

3. **Preserve templating customization** — Keep `DiscordOpenAITemplatingTransmuter.cs` in Sample 01 to demonstrate transmuter customization, which is orthogonal to the routing pattern.

---

## Migration

### Files to Remove

From `01.DiscordAgent`:
- `RouterBlock.cs` — Replaced by covenant routes

From `02.DeclarativeDiscordAgent`:
- Entire directory

### Changes to `01.DiscordAgent/Program.cs`

**Before** (imperative):
```csharp
builder.Services.AddDiscordChat(discordConfig);
builder.Services.AddOpenAIAgents(openAiConfig, registration =>
{
    registration.EnableStreaming();
});

// Window policy overrides...
// Transmuter override...

builder.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, RouterBlock>().Done());
```

**After** (declarative):
```csharp
builder.Services.AddCoven(coven =>
{
    BranchManifest chat = coven.UseDiscordChat(discordConfig);
    BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(
                    new AgentPrompt(msg.Sender, msg.Text)));

            c.Route<AgentResponse, ChatEfferentDraft>(
                (r, ct) => Task.FromResult(
                    new ChatEfferentDraft("BOT", r.Text)));

            c.Route<AgentAfferentChunk, ChatChunk>(
                (chunk, ct) => Task.FromResult(
                    new ChatChunk("BOT", chunk.Text)));

            c.Terminal<AgentThought>();
            c.Terminal<AgentAfferentThoughtChunk>();
        });
});
```

### What's Preserved

| Concern | Status | Notes |
|---------|--------|-------|
| Window policy overrides | **Keep** | Demonstrates `IWindowPolicy<T>` customization |
| `DiscordOpenAITemplatingTransmuter` | **Keep** | Demonstrates transmuter customization |
| FileScrivener configuration | **Keep** | Journal persistence is orthogonal |
| Environment variable config | **Keep** | Same configuration pattern |

---

## README Updates

The `01.DiscordAgent/README.md` needs updates to:

1. Remove references to `RouterBlock.cs` and imperative patterns
2. Describe the declarative covenant approach
3. Remove the "Key files" section entry for `RouterBlock.cs`
4. Update the architecture description to reflect covenant-based routing

The sample diagram may need updating if it depicts the RouterBlock flow.

---

## Alternatives Considered

### Keep Both Samples

**Rejected.** The imperative pattern is not a recommended alternative—it's strictly worse. Maintaining it suggests equivalence where none exists. Users who need custom routing logic can still implement `IMagikBlock` directly; they don't need a sample teaching them the boilerplate-heavy approach.

### Create Sample 03+ for Other Scenarios

**Deferred.** Future samples should demonstrate genuinely different scenarios (e.g., multi-agent orchestration, different chat backends), not alternative routing patterns. This proposal focuses on consolidation; new sample topics are out of scope.

### Rename 02 to 01 Instead of Porting

**Rejected.** Sample 01 demonstrates valuable customizations (transmuter, window policies) that 02 doesn't include. Porting preserves these while adopting the better routing pattern.

---

## Checklist

- [ ] Update `01.DiscordAgent/Program.cs` to declarative covenant pattern
- [ ] Remove `01.DiscordAgent/RouterBlock.cs`
- [ ] Update `01.DiscordAgent/README.md`
- [ ] Remove `02.DeclarativeDiscordAgent/` directory entirely
- [ ] Update `Coven.sln` to remove the 02 project reference
- [ ] Verify sample builds and runs
- [ ] Update any cross-references in documentation pointing to Sample 02
