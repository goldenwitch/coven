# Sample 01 — Discord Agent

Run a Discord-backed chat agent powered by OpenAI, wired together using Coven’s composable runtime.

## What This Is (Coven terms)

- Chat Adapter: Uses `Coven.Chat.Discord` to turn a Discord channel into a stream of `ChatEntry` events (afferent = inbound user messages, efferent = bot drafts/outputs).
- Agent Integration: Uses `Coven.Agents.OpenAI` to map chat to `AgentEntry` prompts/thoughts/responses. Streaming is enabled for responsive output.
- Router (MagikBlock): `RouterBlock` is a simple `IMagikBlock` that bridges chat ↔ agents by reading/writing via `IScrivener<T>` logs (journal-first design).
- Daemons: Discord and OpenAI run as `ContractDaemon`s managed by the host lifecycle (start/shutdown cooperatively).
- Semantic windowing: Output chunking is governed by `IWindowPolicy<AgentAfferentChunk>` for paragraph-first aggregation and max-length capping.
- Transmutation: `DiscordOpenAITemplatingTransmuter` customizes how OpenAI request/response items are templated (e.g., decorating with Discord username/model markers).

![Diagram demonstrating how the 01 sample connects agents to Discord so they can answer user questions.](<../../../assets/Sample 01 Diagram.svg>)

Key files:
- `Program.cs`: configuration, DI registration, window policies, and Coven wiring.
- `RouterBlock.cs`: the chat ↔ agent bridge.
- `DiscordOpenAITemplatingTransmuter.cs`: optional OpenAI request/response templating.


## Setup

Prerequisites:
- .NET 10 SDK installed.
- Discord Bot: token provisioned, bot invited to your server, Message Content Intent enabled, and permission to read/write in a target channel.
- Channel ID: enable Discord Developer Mode → right‑click channel → Copy ID.
- OpenAI: API key and a valid model (e.g., `gpt-5-2025-08-07`).

Configure secrets (env vars preferred):
- `DISCORD_BOT_TOKEN`
- `DISCORD_CHANNEL_ID` (unsigned integer)
- `OPENAI_API_KEY`
- `OPENAI_MODEL` (defaults to `gpt-5-2025-08-07` if not set)

Alternatively, edit defaults at the top of `Program.cs`; these are used only when env vars are absent:

```csharp
string defaultDiscordToken = ""; // Discord bot token
ulong defaultDiscordChannelId = 0; // channel id
string defaultOpenAiApiKey = ""; // OpenAI API key
string defaultOpenAiModel = "gpt-5-2025-08-07"; // model
```

Run the sample:
- From repo root: `dotnet run --project src/samples/01.DiscordAgent -c Release`
- The app starts Discord and OpenAI daemons, then bridges the configured channel. Type in the channel; the bot responds there.

Troubleshooting:
- Discord: Verify the bot is in the server, has access to the channel, Message Content Intent is enabled, and `ChannelId` is correct.
- OpenAI: Confirm API key and model are valid for your account.
- Networking: Ensure outbound HTTPS to Discord and OpenAI is allowed.

### Persistence (FileScrivener)
- Journals persist to NDJSON files for replay/audit:
  - Chat: `./data/discord-chat.ndjson`
  - Agent: `./data/openai-agent.ndjson`
- Change paths by editing `AddFileScrivener<ChatEntry|AgentEntry>(new FileScrivenerConfig { FilePath = ... })` in `Program.cs`.

## Extend

Swap Discord for Console chat (one-line change):

```csharp
// Replace
builder.Services.AddDiscordChat(discordConfig);
// with
builder.Services.AddConsoleChat(new ConsoleClientConfig
{
    InputSender = "console",
    OutputSender = "BOT"
});
```

Tune output windowing (paragraph-first + tighter cap):

```csharp
builder.Services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
    new CompositeWindowPolicy<AgentAfferentChunk>(
        new AgentParagraphWindowPolicy(),
        new AgentMaxLengthWindowPolicy(1024)));
```

Surface agent thoughts to the chat (optional):

```csharp
// RouterBlock.cs
case AgentThought t:
    // Uncomment to stream thoughts
    // await _chat.WriteAsync(new ChatEfferentDraft("BOT", t.Text), cancellationToken);
    break;
```

Customize OpenAI request/response templating:

```csharp
// DiscordOpenAITemplatingTransmuter.cs (maps OpenAIEntry → ResponseItem)
OpenAIEfferent u => ResponseItem.CreateUserMessageItem(
    $"[discord username:{u.Sender}] {u.Text}");
OpenAIAfferent a => ResponseItem.CreateAssistantMessageItem(
    $"[assistant:{a.Model}] {a.Text}");
```

Adjust model behavior (example: increase reasoning effort):

```csharp
OpenAIClientConfig openAiConfig = new()
{
    ApiKey = "<your-openai-api-key>",
    Model = "gpt-5-2025-08-07",
    Reasoning = new ReasoningConfig { Effort = ReasoningEffort.High }
};
```
Verify organization for reasoning summary to work

Go to https://platform.openai.com/settings/organization/general and click on the verify organization button. Openai will ask you to use an ID verification app to take pictures of an official ID such as a drivers lisence to verify your account. This will allow the reasoning summary to work.