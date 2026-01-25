# Coven.Testing.Harness

Black-box E2E test infrastructure that virtualizes external dependencies (console I/O, Discord, OpenAI) to enable deterministic integration testing without real services.

## Key Components

| Component | Purpose |
|-----------|---------|
| `E2ETestHostBuilder` | Fluent builder for configuring test scenarios |
| `E2ETestHost` | Manages test lifecycle (start, run, dispose) |
| `VirtualConsoleIO` | Virtualizes stdin/stdout for console chat testing |
| `VirtualDiscordGateway` | Scripts Discord messages and captures bot responses |
| `VirtualOpenAIGateway` | Scripts OpenAI API responses |
| `JournalAccessor` | Inspects journal entries after test execution |

## Usage Pattern

```csharp
[Fact]
public async Task Bot_responds_to_greeting()
{
    // 1. Build the test host with scripted dependencies
    await using var host = await new E2ETestHostBuilder()
        .WithConsole()
        .WithOpenAI(openai => openai
            .ScriptResponse("Hello! How can I help you?"))
        .BuildAsync();

    // 2. Execute the scenario
    host.Console.WriteLine("Hi there");
    await host.RunUntilIdleAsync();

    // 3. Assert on outputs
    var output = host.Console.ReadAllOutput();
    Assert.Contains("Hello!", output);
}
```

## Virtual Gateways

Each gateway allows scripting responses that the system under test will receive:

```csharp
// Script multiple OpenAI responses in sequence
.WithOpenAI(openai => openai
    .ScriptResponse("First response")
    .ScriptResponse("Second response"))

// Script Discord messages from virtual users
.WithDiscord(discord => discord
    .ScriptMessage(channelId: 123, userId: 456, "Hello bot!"))
```

## Journal Inspection

Access journal entries to verify internal state:

```csharp
var journal = host.GetJournalAccessor();
var entries = await journal.GetEntriesAsync();
Assert.Single(entries, e => e.Contains("expected content"));
```

## Examples

See [`Coven.E2E.Tests`](../Coven.E2E.Tests/) for real-world test scenarios demonstrating all harness capabilities.
