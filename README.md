# Coven

A minimal, composable **.NET 10** engine for orchestrating multiple agents to achieve big things.

> ___"With great power comes great responsibility"___ - _Uncle Ben_
> <br> If you use this library, don't be evil.

## Covenants

* **Journal or it didn't happen** Every thought and output lands in a Scrivener for replay, audit, and time-travel.
* **Compile time validation is better than vibes** Designed from the ground up to minimize side-effects.
* **Daemons behave** Lifecycle, backpressure, graceful shutdown. Async and long-running by design.
* **Hosts over ceremony** Use generic host and DI patterns to painlessly replace or extend functionality.
* **Window/Shatter** Semantic windowing over streamed chats and agents.

## Quick Start
Run Sample 01 (Discord Agent) to see Coven orchestrate a Discord chat channel with an OpenAI‑backed agent.

See detailed steps: [Sample 01 — Discord Agent README](src/samples/01.DiscordAgent/README.md).

![Diagram demonstrating how the 01 sample connects agents to Discord so they can answer user questions.](<assets/Sample 01 Diagram.svg>)

- Prerequisites:
  - .NET 10 SDK installed.
  - Discord Bot: token provisioned, bot invited to your server, Message Content Intent enabled in the Discord Developer Portal, and permission to read/write in a target channel.
  - Channel ID: enable Discord Developer Mode, right‑click the target channel → Copy ID.
  - OpenAI API key and a valid model (for example, `gpt-5-2025-08-07`).

### 1) Configure secrets (env vars or defaults)

- Easiest: set environment variables and keep `Program.cs` unchanged:
  - `DISCORD_BOT_TOKEN`
  - `DISCORD_CHANNEL_ID` (unsigned integer)
  - `OPENAI_API_KEY`
  - `OPENAI_MODEL` (defaults to `gpt-5-2025-08-07` if not set)
- Or edit defaults at the top of `src/samples/01.DiscordAgent/Program.cs` (they’re used only if env vars are absent).

Example from Sample 01 (`Program.cs`):

```csharp
// Defaults used if env vars are not present
string defaultDiscordToken = ""; // set your Discord bot token
ulong defaultDiscordChannelId = 0; // set your channel id
string defaultOpenAiApiKey = ""; // set your OpenAI API key
string defaultOpenAiModel = "gpt-5-2025-08-07"; // choose the model

// Environment overrides (optional)
string? envDiscordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
string? envDiscordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
string? envOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
string? envOpenAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

ulong channelId = defaultDiscordChannelId;
if (!string.IsNullOrWhiteSpace(envDiscordChannelId) && ulong.TryParse(envDiscordChannelId, out ulong parsed))
{
    channelId = parsed;
}

DiscordClientConfig discordConfig = new()
{
    BotToken = string.IsNullOrWhiteSpace(envDiscordToken) ? defaultDiscordToken : envDiscordToken,
    ChannelId = channelId
};

OpenAIClientConfig openAiConfig = new()
{
    ApiKey = string.IsNullOrWhiteSpace(envOpenAiApiKey) ? defaultOpenAiApiKey : envOpenAiApiKey,
    Model = string.IsNullOrWhiteSpace(envOpenAiModel) ? defaultOpenAiModel : envOpenAiModel
};
```

### 2) Wire up Discord + OpenAI and run

- From repo root: `dotnet run --project src/samples/01.DiscordAgent -c Release`
- The app starts Discord and OpenAI daemons, then bridges chat↔agent in the configured channel. Type in the channel; the bot replies there.

Minimal wiring from Sample 01 (`Program.cs`):

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddDiscordChat(discordConfig);
builder.Services.AddOpenAIAgents(openAiConfig, registration =>
{
    registration.EnableStreaming();
});

// Optional: override OpenAI mapping with templating
builder.Services.AddScoped<ITransmuter<OpenAIEntry, ResponseItem>, DiscordOpenAITemplatingTransmuter>();

// Define declarative covenant routes (validated at build time)
builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseDiscordChat(discordConfig);
    BranchManifest agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            // Chat → Agents: incoming messages become prompts
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));

            // Agents → Chat: responses become outgoing draft messages
            c.Route<AgentResponse, ChatEfferentDraft>(
                (r, ct) => Task.FromResult(new ChatEfferentDraft("BOT", r.Text)));

            // Streaming chunks: real-time display
            c.Route<AgentAfferentChunk, ChatChunk>(
                (chunk, ct) => Task.FromResult(new ChatChunk("BOT", chunk.Text)));

            // Thoughts are terminal (not displayed to users)
            c.Terminal<AgentThought>();
        });
});

IHost host = builder.Build();

// Execute ritual - daemons auto-start via CovenExecutionScope
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());
```

### Troubleshooting

- Discord: If no messages appear, verify the bot has access to the channel, Message Content Intent is enabled, and `ChannelId` is correct.
- OpenAI: If errors occur on first response, confirm the API key and model name are valid for your account.
- Networking: Corporate proxies/firewalls can block Discord/OpenAI APIs; ensure outbound HTTPS is allowed.

### Extensibility

Semantic windowing: policies define when streamed messages are ready for decision‑making (not fixed turns). See: `architecture/Windowing-and-Shattering.md`.

```csharp
// Paragraph-first + tighter max-length for agent outputs
builder.Services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentChunk>(
        new AgentParagraphWindowPolicy(),
        new AgentMaxLengthWindowPolicy(1024)));

// Optionally tune thought chunking independently
// builder.Services.AddScoped<IWindowPolicy<AgentAfferentThoughtChunk>>(_ =>
//     new CompositeWindowPolicy<AgentAfferentThoughtChunk>(
//         new AgentThoughtSummaryMarkerWindowPolicy(),
//         new AgentThoughtMaxLengthWindowPolicy(2048)));
```

Custom OpenAI templating: override prompt/response item mapping to inject context (from `DiscordOpenAITemplatingTransmuter.cs`). Callers filter entries before transmutation:

```csharp
internal sealed class DiscordOpenAITemplatingTransmuter : ITransmuter<OpenAIEntry, ResponseItem>
{
    public Task<ResponseItem> Transmute(OpenAIEntry Input, CancellationToken cancellationToken = default)
    {
        return Input switch
        {
            OpenAIEfferent u => Task.FromResult(
                ResponseItem.CreateUserMessageItem($"[discord username:{u.Sender}] {u.Text}")),
            OpenAIAfferent a => Task.FromResult(
                ResponseItem.CreateAssistantMessageItem($"[assistant:{a.Model}] {a.Text}")),
            _ => throw new ArgumentOutOfRangeException(nameof(Input),
                $"Cannot transmute {Input.GetType().Name}. Filter before transmuting.")
        };
    }
}
```

Surface agent thoughts: optionally echo internal thinking to the chat:

```csharp
// In covenant routes, replace Terminal<AgentThought>() with:
c.Route<AgentThought, ChatEfferentDraft>(
    (t, ct) => Task.FromResult(
        new ChatEfferentDraft("BOT", t.Text)));
```
### I don't want to make a discord bot.
Don't use Discord? No problem. One line change to swap to using Console as your chat of choice.
```csharp
// Replace
builder.Services.AddDiscordChat(discordConfig);
// with
builder.Services.AddConsoleChat(new ConsoleClientConfig
{
    InputSender = "console",
    OutputSender = "BOT"
});

// Keep OpenAI registration as-is
builder.Services.AddOpenAIAgents(openAiConfig);
```

### I want to configure my model to do different things
You can use any settings available on the OpenAIClientConfig. For example, you could make the model chew longer by setting Effort = ReasoningEffort.High

```csharp
OpenAIClientConfig openAiConfig = new()
{
    ApiKey = "<your-openai-api-key>",
    Model = "gpt-5-2025-08-07",
    Reasoning = new ReasoningConfig { Effort = ReasoningEffort.High }
};

// Then register
builder.Services.AddOpenAIAgents(openAiConfig);
```

## Overview
Ever felt like it was too hard to get products that you pay for to talk to each other? Perhaps felt like they should just work together... magically? :P

You are in the right place.

### Structure
Every Coven is organized into a "spine" of MagikBlocks, executing one after the other.
Each MagikBlock execution represents a unique scope with a fixed input and output type.
> _Cheatcodes_: Use Empty as an input if you want to route to a MagikBlock with no inputs.

![Diagram showing what a Coven "Branch" is. It shows a Spine Segment (where user code lives) connecting to a "branch" abstraction, isolating the user code from the integrations.](<assets/Normal Looking Branch.svg>)

By starting Daemons and reading journals, your block executes the logic it needs, abstracted from the downstream implementation. The layers that define these abstractions are the "branches" that stretch off of your MagikBlock's execution. Coven offers two convenient abstractions:
- **Coven.Chat**: Multi-user conversations.
- **Coven.Agents**: Working with an AI powered Agent to complete your goals.

Built on the other side of the "branch" abstractions are Coven's handcrafted integrations with external systems. These integrations are like the "leaves" of our twisted tree, they translate Coven standard abstractions to an external system.
- **Coven.Chat.Discord**: Use discord to chat with your Coven.
- **Coven.Chat.Console**: Use a terminal to chat with your Coven.
- **Coven.Agents.OpenAI**: Send requests to an agent from your Coven.

![Diagram showing how multiple Branches and Leaves can be connected to each Spine Segment.](<assets/Normal Looking Spine Segment.svg>)

![Diagram showing how multiple spine segments can be used to create complex flows where isolating each part of the flow to a single segment can remove complexity around the states of your application.](<assets/Normal Looking Coven.svg>)

### Why use Coven?
Anyone can write new branches or leaves and they will seamlessly integrate with your software.

Alternatively, because we are the easiest way to get agents to collaborate with users and each other.

### Vocabulary Cheatsheet
> Core
- MagikBlock: a unit of work with `DoMagik` that reads/writes journals.
- Daemon (`ContractDaemon`): long‑running background service started by a block.
- Scrivener (`IScrivener<T>`): append-only journal for typed entries; supports tailing.
- Transmuter: pure mapping between types; `IBiDirectionalTransmuter` supports both directions.
- Ritual: an invocation that executes a pipeline of MagikBlocks.
- Entry: a record written to a journal (e.g., `ChatEntry`, `AgentEntry`).

> Streaming and Window/Shatter
- Window Policy: rules that group stream chunks into windows for emission.
- Shatter Policy: rules that split entries into smaller chunks for windowing.
- Chunk: stream fragment (e.g., `AgentAfferentChunk`, `AgentAfferentThoughtChunk`).
- Batch Transmuter: combines a window of chunks into an output (response or thought).

> Structure
- Leaf: Connects your currently executing block to an external system. Lives at the end of a branch.
- Branch: Services that connect your currently executing block to an external system via an abstraction. For example, Coven.Agents and Coven.Chat
- Spine: Your executing ritual. Each vertebrae is a MagikBlock in your ritual.
- Afferent/Efferent: The direction that a message is traveling. 
    - Efferent: from spine to leaf.
    - Afferent: from leaf to spine.

## Licensing

**Dual‑license (BUSL‑1.1 + Commercial):**

* **Community**: Business Source License 1.1 (BUSL‑1.1) with an Additional Use Grant permitting Production Use if you and your affiliates made **< US $100M** in combined gross revenue in the prior fiscal year. See `LICENSE`.
* **Commercial/Enterprise**: available under a separate agreement. See `COMMERCIAL-TERMS.md`.

*Change Date/License*: `LICENSE` specifies a Change License of **MIT** on **2029‑09‑11**.

## Support

* Patreon: [https://www.patreon.com/c/Goldenwitch](https://www.patreon.com/c/Goldenwitch)

> © 2025 Autumn Wyborny. BUSL 1.1, free for non-profits, individuals, and commercial business under 100m annual revenue.
