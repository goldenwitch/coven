# Declarative Discord Agent

This sample demonstrates the **declarative covenants** pattern, replacing the imperative RouterBlock approach from Sample 01.

## What's Different

### Before (Sample 01 - Imperative)

```csharp
// RouterBlock.cs - ~60 lines of boilerplate
internal sealed class RouterBlock(
    IScrivener<ChatEntry> chat,
    IScrivener<AgentEntry> agents) : IMagikBlock<Empty, Empty>
{
    public async Task<Empty> DoMagik(Empty input, CancellationToken ct)
    {
        // Two parallel pumps with manual pattern matching
        Task chatToAgents = Task.Run(async () =>
        {
            await foreach ((long _, ChatEntry? entry) in _chat.TailAsync(0, ct))
            {
                if (entry is ChatAfferent inc)
                    await _agents.WriteAsync(new AgentPrompt(...), ct);
            }
        }, ct);
        
        Task agentsToChat = Task.Run(async () =>
        {
            await foreach ((long _, AgentEntry? entry) in _agents.TailAsync(0, ct))
            {
                switch (entry)
                {
                    case AgentResponse r: // route to chat
                    case AgentThought t:  // terminal or route
                }
            }
        }, ct);
        
        await Task.WhenAll(chatToAgents, agentsToChat);
    }
}
```

### After (This Sample - Declarative)

```csharp
// Program.cs only - no RouterBlock needed
builder.Services.AddCoven(coven =>
{
    var chat = coven.UseDiscordChat(discordConfig);
    var agents = coven.UseOpenAIAgents(openAiConfig, reg => reg.EnableStreaming());

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            c.Route<ChatAfferent, AgentPrompt>((msg, ct) => 
                Task.FromResult(new AgentPrompt(msg.Sender, msg.Text)));
            
            c.Route<AgentResponse, ChatEfferentDraft>((r, ct) => 
                Task.FromResult(new ChatEfferentDraft("BOT", r.Text)));
            
            c.Route<AgentAfferentChunk, ChatChunk>((chunk, ct) => 
                Task.FromResult(new ChatChunk("BOT", chunk.Text)));
            
            c.Terminal<AgentThought>();
            c.Terminal<AgentAfferentThoughtChunk>();
        });
});
```

## Benefits

1. **Build-time validation** — Missing routes or terminals fail at startup, not at runtime
2. **No boilerplate** — No RouterBlock class, no manual journal tailing, no Task.Run pumps
3. **Self-documenting** — The routing table is visible at registration time
4. **Type-safe** — Routes are generic; mismatched types are compile errors

## Configuration

Same environment variables as Sample 01:

| Variable | Description |
|----------|-------------|
| `DISCORD_BOT_TOKEN` | Your Discord bot token |
| `DISCORD_CHANNEL_ID` | Channel ID for the bot to operate in |
| `OPENAI_API_KEY` | Your OpenAI API key |
| `OPENAI_MODEL` | Model to use (default: gpt-5-2025-08-07) |

## Running

```bash
cd src/samples/02.DeclarativeDiscordAgent
dotnet run
```

## Related

- [Sample 01 - DiscordAgent](../01.DiscordAgent/README.md) — Imperative RouterBlock approach
- [Declarative Covenants Proposal](../../../proposals/declarative-covenants.md) — Design rationale
