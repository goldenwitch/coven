# Coven.Chat.Console

Wires stdin/stdout into Coven as `ChatEntry` using the same journal+transmuter patterns as the Discord adapter.

## Overview
- Reads user input from stdin as `ConsoleIncoming` and transmutes to `ChatIncoming`.
- Writes `ChatOutgoing` as `ConsoleOutgoing` and prints to stdout; records `ConsoleAck`/`ChatAck` to avoid loops.
- Uses `IScrivener<T>` journals and an `IBiDirectionalTransmuter` to bridge Console↔Chat.

## DI Registration
```csharp
using Coven.Chat.Console;
using Microsoft.Extensions.DependencyInjection;

services.AddConsoleChat(new ConsoleClientConfig {
    InputSender = "console",
    OutputSender = "BOT"
});
```

Registered services:
- `IScrivener<ChatEntry>`: default in-memory if not provided.
- `IScrivener<ConsoleEntry>`: `ConsoleScrivener` over a keyed internal `InMemoryScrivener`.
- `ConsoleGatewayConnection`: stdin/out bridge.
- `ConsoleTransmuter`: Console↔Chat mapping.
- `ConsoleChatSessionFactory`, `ConsoleChatSession`, `ConsoleChatDaemon`.

## Lifecycle
Start the daemon and tail the `ChatEntry` journal. Example echo flow:
```csharp
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder b = Host.CreateApplicationBuilder(args);
b.Services.AddConsoleChat(new ConsoleClientConfig { InputSender = "console", OutputSender = "BOT" });
b.Services.BuildCoven(c => c.MagikBlock<Empty, Empty, EchoBlock>().Done());
IHost host = b.Build();
ICoven coven = host.Services.GetRequiredService<ICoven>();
await coven.Ritual<Empty, Empty>(new Empty());

sealed class EchoBlock(ContractDaemon daemon, IScrivener<ChatEntry> journal) : IMagikBlock<Empty, Empty>
{
    public async Task<Empty> DoMagik(Empty _, CancellationToken ct = default)
    {
        await daemon.Start(ct);
        await foreach ((_, ChatEntry e) in journal.TailAsync(0, ct))
            if (e is ChatIncoming i)
                await journal.WriteAsync(new ChatOutgoing("BOT", i.Text), ct);
        return _;
    }
}
```

## Notes
- Line-delimited input: each stdin line becomes one message.
- Acks are not pumped across journals; they only confirm local writes.
- All operations honor cooperative cancellation.

