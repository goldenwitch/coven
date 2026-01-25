# Proposal: Discord Gateway Abstraction

## Status
Draft

## Summary

Extract `IDiscordGateway` interface from the concrete `DiscordGatewayConnection` to decouple Discord-specific business logic from the `DiscordSocketClient` implementation. This enables test virtualization, alternative Discord client libraries, and cleaner separation of concerns between transport and domain logic.

## Motivation

### Current Coupling

The `DiscordGatewayConnection` class directly depends on `DiscordSocketClient` from Discord.Net, creating several issues:

1. **Untestable without real Discord** — No seam exists to inject a fake gateway for unit or E2E tests
2. **Event-driven inbound flow** — Uses `MessageReceived` event handler, forcing callback-based message processing rather than pull-based `IAsyncEnumerable`
3. **Complex channel resolution** — Cache-first + REST fallback logic is baked into `SendAsync()`, mixing transport concerns with retry/resolution strategy
4. **Bot self-filtering scattered** — Bot message filtering happens in `OnMessageReceivedAsync()`, but this policy decision could reasonably live elsewhere

### Blocking Dependency

The [E2E Test Harness proposal](e2e-test-harness.md) requires a `VirtualDiscordGateway` to simulate Discord messages in tests. That implementation cannot exist until this abstraction is in place:

> **From E2E Test Harness proposal:**
> "E2E tests for Discord-based covenants are blocked until this abstraction exists. The `VirtualDiscordGateway` described in this proposal cannot be implemented against the current concrete coupling."

### Independent Value

Beyond testing, this abstraction enables:

- **Rate-limit simulation** — Virtual gateway can inject artificial delays for testing rate-limit handling
- **Alternative libraries** — Could support DSharpPlus or other Discord libraries without changing business logic
- **Replay/determinism** — Record real Discord sessions and replay them for regression testing
- **Offline development** — Develop Discord features without a live bot connection

## Design

### Interface Definition

```csharp
// src/Coven.Chat.Discord/IDiscordGateway.cs

/// <summary>
/// Abstracts Discord connectivity for inbound message reception and outbound message dispatch.
/// Implementations may connect to real Discord (via Discord.Net) or provide virtualized
/// gateways for testing.
/// </summary>
public interface IDiscordGateway : IAsyncDisposable
{
    /// <summary>
    /// Establishes the Discord connection and begins receiving messages.
    /// For real gateways, this performs login and starts the WebSocket connection.
    /// Returns when the gateway is ready to send/receive messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the connection attempt.</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronous stream of inbound Discord messages. The stream yields messages
    /// as they arrive and completes when the gateway disconnects or is disposed.
    /// </summary>
    /// <remarks>
    /// Implementations should filter bot-authored messages before yielding, unless
    /// <see cref="DiscordGatewayOptions.IncludeBotMessages"/> is explicitly set.
    /// </remarks>
    IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends a message to the specified Discord channel.
    /// </summary>
    /// <param name="channelId">The target channel ID.</param>
    /// <param name="content">The message content to send.</param>
    /// <param name="cancellationToken">Cancellation token for the send operation.</param>
    Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken);
}
```

### Inbound Message Record

```csharp
// src/Coven.Chat.Discord/DiscordInboundMessage.cs

/// <summary>
/// Represents a message received from Discord, normalized for Coven processing.
/// </summary>
/// <param name="ChannelId">The Discord channel where the message was posted.</param>
/// <param name="Author">The username of the message author.</param>
/// <param name="Content">The message text content.</param>
/// <param name="MessageId">Discord's unique message identifier (snowflake as string).</param>
/// <param name="Timestamp">When the message was created on Discord.</param>
/// <param name="IsBot">Whether the author is a bot account.</param>
public sealed record DiscordInboundMessage(
    ulong ChannelId,
    string Author,
    string Content,
    string MessageId,
    DateTimeOffset Timestamp,
    bool IsBot);
```

### Gateway Configuration Options

```csharp
// src/Coven.Chat.Discord/DiscordGatewayOptions.cs

/// <summary>
/// Configuration options for Discord gateway behavior.
/// </summary>
public sealed record DiscordGatewayOptions
{
    /// <summary>
    /// When true, bot-authored messages are included in the inbound stream.
    /// Default is false—bot messages are filtered to prevent response loops.
    /// </summary>
    public bool IncludeBotMessages { get; init; } = false;

    /// <summary>
    /// Optional channel filter. When set, only messages from these channels
    /// are included in the inbound stream. When null, all channels are included.
    /// </summary>
    public IReadOnlySet<ulong>? ChannelFilter { get; init; }
}
```

### Production Implementation: DiscordNetGateway

The existing `DiscordGatewayConnection` logic is refactored into a new class that implements `IDiscordGateway`:

```csharp
// src/Coven.Chat.Discord/DiscordNetGateway.cs

/// <summary>
/// Production implementation of <see cref="IDiscordGateway"/> using Discord.Net's
/// <see cref="DiscordSocketClient"/>. Converts the event-based message model to
/// <see cref="IAsyncEnumerable{T}"/> and handles channel resolution internally.
/// </summary>
internal sealed class DiscordNetGateway : IDiscordGateway
{
    private readonly DiscordClientConfig _config;
    private readonly DiscordSocketClient _socketClient;
    private readonly DiscordGatewayOptions _options;
    private readonly ILogger<DiscordNetGateway> _logger;
    
    // Channel bridges event callbacks to async enumerable
    private readonly Channel<DiscordInboundMessage> _inboundChannel;
    private bool _connected;

    public DiscordNetGateway(
        DiscordClientConfig config,
        DiscordSocketClient socketClient,
        DiscordGatewayOptions options,
        ILogger<DiscordNetGateway> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _socketClient = socketClient ?? throw new ArgumentNullException(nameof(socketClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _inboundChannel = Channel.CreateUnbounded<DiscordInboundMessage>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connected)
            throw new InvalidOperationException("Gateway is already connected.");

        DiscordLog.Connecting(_logger, _config.ChannelId);
        cancellationToken.ThrowIfCancellationRequested();

        _socketClient.MessageReceived += OnMessageReceivedAsync;

        await _socketClient.LoginAsync(TokenType.Bot, _config.BotToken).ConfigureAwait(false);
        await _socketClient.StartAsync().ConfigureAwait(false);
        
        _connected = true;
        DiscordLog.Connected(_logger);
    }

    public async IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _inboundChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public async Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(content))
            return;

        IMessageChannel messageChannel = await ResolveChannelAsync(channelId, cancellationToken)
            .ConfigureAwait(false);

        DiscordLog.OutboundSendStart(_logger, channelId, content.Length);
        
        try
        {
            await messageChannel.SendMessageAsync(content)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            DiscordLog.OutboundSendSucceeded(_logger, channelId);
        }
        catch (OperationCanceledException)
        {
            DiscordLog.OutboundOperationCanceled(_logger, channelId);
            throw;
        }
        catch (Exception ex)
        {
            DiscordLog.OutboundSendFailed(_logger, channelId, ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _socketClient.MessageReceived -= OnMessageReceivedAsync;
        _inboundChannel.Writer.TryComplete();
        
        if (_connected)
        {
            await _socketClient.StopAsync().ConfigureAwait(false);
            await _socketClient.LogoutAsync().ConfigureAwait(false);
        }
    }

    private Task OnMessageReceivedAsync(SocketMessage message)
    {
        // Apply bot filtering unless explicitly included
        if (message.Author.IsBot && !_options.IncludeBotMessages)
        {
            DiscordLog.InboundBotMessageObserved(_logger, message.Author.Username, 
                message.Content?.Length ?? 0);
            return Task.CompletedTask;
        }

        // Apply channel filter if configured
        if (_options.ChannelFilter is not null && 
            !_options.ChannelFilter.Contains(message.Channel.Id))
        {
            return Task.CompletedTask;
        }

        var inbound = new DiscordInboundMessage(
            ChannelId: message.Channel.Id,
            Author: message.Author.Username,
            Content: message.Content ?? string.Empty,
            MessageId: message.Id.ToString(CultureInfo.InvariantCulture),
            Timestamp: message.Timestamp,
            IsBot: message.Author.IsBot);

        DiscordLog.InboundUserMessageReceived(_logger, inbound.Author, inbound.Content.Length);
        
        // Non-blocking write; channel is unbounded
        _inboundChannel.Writer.TryWrite(inbound);
        
        return Task.CompletedTask;
    }

    private async Task<IMessageChannel> ResolveChannelAsync(
        ulong channelId, 
        CancellationToken cancellationToken)
    {
        // Cache-first lookup
        if (_socketClient.GetChannel(channelId) is IMessageChannel cachedChannel)
        {
            DiscordLog.ChannelCacheHit(_logger, channelId);
            return cachedChannel;
        }

        // REST fallback for cold cache
        DiscordLog.ChannelCacheMiss(_logger, channelId);
        DiscordLog.ChannelRestFetchStart(_logger, channelId);

        try
        {
            var requestOptions = new RequestOptions { CancelToken = cancellationToken };
            var restChannel = await _socketClient.Rest
                .GetChannelAsync(channelId, requestOptions)
                .ConfigureAwait(false);

            if (restChannel is IMessageChannel resolvedChannel)
            {
                DiscordLog.ChannelRestFetchSuccess(_logger, channelId);
                return resolvedChannel;
            }

            var actualType = restChannel?.GetType().Name ?? "null";
            DiscordLog.ChannelRestFetchInvalidType(_logger, channelId, actualType);
            throw new InvalidOperationException(
                $"Channel '{channelId}' is not a message channel (actual: {actualType}).");
        }
        catch (OperationCanceledException)
        {
            DiscordLog.ChannelLookupCanceled(_logger, channelId);
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            DiscordLog.ChannelLookupError(_logger, channelId, ex);
            throw;
        }
    }
}
```

### Refactored DiscordGatewayConnection

The original `DiscordGatewayConnection` becomes a thin adapter that consumes `IDiscordGateway` and writes to the internal scrivener:

```csharp
// src/Coven.Chat.Discord/DiscordGatewayConnection.cs (refactored)

/// <summary>
/// Bridges <see cref="IDiscordGateway"/> to the Discord journal by pumping inbound
/// messages to the internal scrivener. Outbound sends are delegated to the gateway.
/// </summary>
internal sealed class DiscordGatewayConnection : IAsyncDisposable
{
    private readonly DiscordClientConfig _config;
    private readonly IDiscordGateway _gateway;
    private readonly IScrivener<DiscordEntry> _scrivener;
    private readonly ILogger<DiscordGatewayConnection> _logger;
    
    private Task? _inboundPump;
    private CancellationTokenSource? _pumpCts;

    public DiscordGatewayConnection(
        DiscordClientConfig config,
        IDiscordGateway gateway,
        [FromKeyedServices("Coven.InternalDiscordScrivener")] IScrivener<DiscordEntry> scrivener,
        ILogger<DiscordGatewayConnection> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _scrivener = scrivener ?? throw new ArgumentNullException(nameof(scrivener));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _gateway.ConnectAsync(cancellationToken).ConfigureAwait(false);
        
        _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inboundPump = PumpInboundMessagesAsync(_pumpCts.Token);
    }

    public Task SendAsync(string text, CancellationToken cancellationToken)
    {
        return _gateway.SendMessageAsync(_config.ChannelId, text, cancellationToken);
    }

    private async Task PumpInboundMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _gateway.GetInboundMessagesAsync(cancellationToken))
            {
                var afferent = new DiscordAfferent(
                    Sender: message.Author,
                    Text: message.Content,
                    MessageId: message.MessageId,
                    Timestamp: message.Timestamp);

                long position = await _scrivener.WriteAsync(afferent, cancellationToken)
                    .ConfigureAwait(false);
                DiscordLog.InboundAppendedToJournal(_logger, nameof(DiscordAfferent), position);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _pumpCts?.Cancel();
        
        if (_inboundPump is not null)
        {
            try
            {
                await _inboundPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }
        
        _pumpCts?.Dispose();
        await _gateway.DisposeAsync().ConfigureAwait(false);
    }
}
```

### DI Registration Changes

```csharp
// src/Coven.Chat.Discord/DiscordChatServiceCollectionExtensions.cs (updated)

public static IServiceCollection AddDiscordChat(
    this IServiceCollection services, 
    DiscordClientConfig discordClientConfig,
    DiscordGatewayOptions? gatewayOptions = null)
{
    ArgumentNullException.ThrowIfNull(services);

    // Configuration
    services.AddScoped(sp => discordClientConfig);
    services.AddScoped(sp => gatewayOptions ?? new DiscordGatewayOptions());
    
    // Discord.Net client (used by production gateway)
    services.AddScoped(_ => new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents =
            GatewayIntents.Guilds |
            GatewayIntents.GuildMessages |
            GatewayIntents.DirectMessages |
            GatewayIntents.MessageContent,
    }));
    
    // Gateway abstraction - can be replaced for testing
    services.AddScoped<IDiscordGateway, DiscordNetGateway>();
    
    // Gateway connection (consumes IDiscordGateway)
    services.AddScoped<DiscordGatewayConnection>();
    services.AddScoped<DiscordChatSessionFactory>();

    // Journals and transmuters (unchanged)
    services.TryAddScoped<IScrivener<ChatEntry>, InMemoryScrivener<ChatEntry>>();
    services.AddScoped<IScrivener<DiscordEntry>, DiscordScrivener>();
    services.AddKeyedScoped<IScrivener<DiscordEntry>, InMemoryScrivener<DiscordEntry>>(
        "Coven.InternalDiscordScrivener");

    services.AddScoped<IImbuingTransmuter<DiscordEntry, long, ChatEntry>, DiscordTransmuter>();
    services.AddScoped<IImbuingTransmuter<ChatEntry, long, DiscordEntry>, DiscordTransmuter>();
    services.AddScoped<IScrivener<DaemonEvent>, InMemoryScrivener<DaemonEvent>>();
    services.AddScoped<ContractDaemon, DiscordChatDaemon>();

    // Windowing and shattering (unchanged)
    services.AddChatWindowing();
    services.TryAddScoped<IShatterPolicy<ChatEntry>>(sp =>
        new ChainedShatterPolicy<ChatEntry>(
            new ChatParagraphShatterPolicy(),
            new ChatChunkMaxLengthShatterPolicy(2000)));
    services.TryAddScoped<IWindowPolicy<ChatChunk>>(_ =>
        new CompositeWindowPolicy<ChatChunk>(
            new ChatParagraphWindowPolicy(),
            new ChatMaxLengthWindowPolicy(2000)));

    return services;
}
```

### Test Gateway Implementation

For use by `Coven.Testing.Harness` (or unit tests with `InternalsVisibleTo`):

```csharp
// src/Coven.Testing.Harness/VirtualDiscordGateway.cs

/// <summary>
/// Virtual Discord gateway for E2E testing. Allows tests to simulate incoming messages
/// and capture outbound messages without a real Discord connection.
/// </summary>
public sealed class VirtualDiscordGateway : IDiscordGateway
{
    private readonly Channel<DiscordInboundMessage> _inbound;
    private readonly List<OutboundMessage> _sent;
    private bool _connected;

    /// <summary>
    /// Creates a new virtual Discord gateway.
    /// </summary>
    public VirtualDiscordGateway()
    {
        _inbound = Channel.CreateUnbounded<DiscordInboundMessage>();
        _sent = new List<OutboundMessage>();
    }

    /// <summary>
    /// Messages sent through this gateway, available for test assertions.
    /// </summary>
    public IReadOnlyList<OutboundMessage> SentMessages => _sent;

    // === Test Input API ===

    /// <summary>
    /// Simulates an incoming Discord message from a user.
    /// </summary>
    public ValueTask SimulateUserMessageAsync(
        ulong channelId,
        string author,
        string content,
        string? messageId = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        return SimulateMessageAsync(
            channelId, author, content, 
            messageId, timestamp, 
            isBot: false, cancellationToken);
    }

    /// <summary>
    /// Simulates an incoming Discord message (user or bot).
    /// </summary>
    public ValueTask SimulateMessageAsync(
        ulong channelId,
        string author,
        string content,
        string? messageId = null,
        DateTimeOffset? timestamp = null,
        bool isBot = false,
        CancellationToken cancellationToken = default)
    {
        var message = new DiscordInboundMessage(
            ChannelId: channelId,
            Author: author,
            Content: content,
            MessageId: messageId ?? Guid.NewGuid().ToString("N"),
            Timestamp: timestamp ?? DateTimeOffset.UtcNow,
            IsBot: isBot);

        return _inbound.Writer.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    /// Signals that no more inbound messages will arrive.
    /// Causes GetInboundMessagesAsync to complete.
    /// </summary>
    public void CompleteInbound() => _inbound.Writer.TryComplete();

    // === IDiscordGateway ===

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DiscordInboundMessage> GetInboundMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_connected)
            throw new InvalidOperationException("Gateway is not connected.");

        await foreach (var message in _inbound.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public Task SendMessageAsync(ulong channelId, string content, CancellationToken cancellationToken)
    {
        if (!_connected)
            throw new InvalidOperationException("Gateway is not connected.");

        _sent.Add(new OutboundMessage(channelId, content, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Represents a message sent through the virtual gateway.
    /// </summary>
    public sealed record OutboundMessage(ulong ChannelId, string Content, DateTimeOffset SentAt);
}
```

## Required Changes to Core Libraries

### New Files

| File | Purpose |
|------|---------|
| `src/Coven.Chat.Discord/IDiscordGateway.cs` | Gateway interface definition |
| `src/Coven.Chat.Discord/DiscordInboundMessage.cs` | Inbound message record |
| `src/Coven.Chat.Discord/DiscordGatewayOptions.cs` | Gateway configuration options |
| `src/Coven.Chat.Discord/DiscordNetGateway.cs` | Production implementation using Discord.Net |

### Modified Files

| File | Changes |
|------|---------|
| `DiscordGatewayConnection.cs` | Refactor to consume `IDiscordGateway` instead of `DiscordSocketClient` |
| `DiscordChatServiceCollectionExtensions.cs` | Register `IDiscordGateway` → `DiscordNetGateway`, accept optional `DiscordGatewayOptions` |
| `DiscordChatSession.cs` | Update `DisposeAsync` to handle `IAsyncDisposable` gateway connection |
| `DiscordScrivener.cs` | No changes required (continues to use `DiscordGatewayConnection`) |

### Assembly Visibility

Add `InternalsVisibleTo` for test harness access:

```xml
<!-- src/Coven.Chat.Discord/Coven.Chat.Discord.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="Coven.Testing.Harness" />
  <InternalsVisibleTo Include="Coven.Chat.Discord.Tests" />
</ItemGroup>
```

## Implementation Phases

### Phase 1: Interface Extraction (Non-Breaking)

1. Create `IDiscordGateway`, `DiscordInboundMessage`, `DiscordGatewayOptions`
2. Create `DiscordNetGateway` implementing `IDiscordGateway`
3. Update DI to register `IDiscordGateway` → `DiscordNetGateway`
4. Add `InternalsVisibleTo` for test projects

**Validation:** Existing tests and samples continue to work unchanged.

### Phase 2: Refactor DiscordGatewayConnection

1. Modify `DiscordGatewayConnection` to inject `IDiscordGateway` instead of `DiscordSocketClient`
2. Move event-to-scrivener pumping logic to consume `GetInboundMessagesAsync()`
3. Delegate `SendAsync()` to `IDiscordGateway.SendMessageAsync()`

**Validation:** All existing behavior preserved; gateway is now injectable.

### Phase 3: Enable E2E Testing

1. Create `VirtualDiscordGateway` in `Coven.Testing.Harness`
2. Write E2E tests for Discord samples using virtual gateway
3. Document test patterns in harness README

**Validation:** Discord E2E tests pass with virtual gateway.

## Alternatives Considered

### Alternative 1: Mock DiscordSocketClient Directly

**Approach:** Use a mocking framework (Moq, NSubstitute) to mock `DiscordSocketClient`.

**Rejected because:**
- `DiscordSocketClient` has complex internal state and event wiring
- Event-based `MessageReceived` is awkward to mock
- REST fallback path (`socketClient.Rest.GetChannelAsync`) requires deep mocking
- Mocks would be brittle and tightly coupled to Discord.Net internals

### Alternative 2: Wrapper Around DiscordSocketClient

**Approach:** Create a thin wrapper that exposes only the methods we use.

**Rejected because:**
- Still event-based (doesn't solve the `IAsyncEnumerable` requirement)
- Wrapper would grow to mirror the full client surface
- No clear boundary between transport and business logic

### Alternative 3: Keep Event Model, Abstract Events

**Approach:** Define `IDiscordGateway` with event handlers instead of `IAsyncEnumerable`.

**Rejected because:**
- Event callbacks don't compose with async/await patterns
- Harder to test (must wire up handlers, capture invocations)
- Inconsistent with Console and other gateways that use streams

### Alternative 4: Interface at Scrivener Level

**Approach:** Instead of abstracting the gateway, abstract at the `IScrivener<DiscordEntry>` level—let tests inject entries directly.

**Rejected because:**
- Doesn't test the gateway ↔ scrivener integration
- Bot-filtering and channel resolution logic would be untested
- E2E harness wants to simulate *Discord behavior*, not bypass it

## Open Questions

### Q1: Should `IDiscordGateway` Be Public?

**Current decision:** Internal with `InternalsVisibleTo` for test projects.

**Argument for public:** Third-party test utilities, alternative gateway implementations.

**Argument for internal:** Gateway is implementation detail; consumers interact via `IScrivener<DiscordEntry>`.

**Resolution:** Start internal, promote to public if external demand emerges.

### Q2: Where Does Bot Filtering Belong?

**Options:**

1. **Gateway level** (current proposal) — Gateway filters before yielding to stream
2. **Consumer level** — `DiscordGatewayConnection` filters when reading from stream
3. **Configurable** — `DiscordGatewayOptions.IncludeBotMessages` (current proposal)

**Current decision:** Configurable at gateway level with default filtering. This matches the existing behavior where filtering happens in `OnMessageReceivedAsync`.

### Q3: Multi-Channel Support

The current design retains `DiscordClientConfig.ChannelId` for outbound messages. Should the gateway support sending to arbitrary channels?

**Current decision:** Yes — `SendMessageAsync(ulong channelId, ...)` accepts any channel ID. The `DiscordClientConfig.ChannelId` is used by higher-level code (e.g., `DiscordGatewayConnection.SendAsync`) to determine the default channel.

### Q4: Reconnection Handling

Discord.Net handles reconnection internally. Should `IDiscordGateway` expose reconnection events?

**Current decision:** No. The `IAsyncEnumerable` model abstracts reconnection—messages continue flowing after reconnect. If reconnection diagnostics are needed, add `IDiscordGateway` events in a future iteration.

## References

- [E2E Test Harness Proposal](e2e-test-harness.md) — Depends on this abstraction for `VirtualDiscordGateway`
- [IOpenAIGatewayConnection](../src/Coven.Agents.OpenAI/IOpenAIGatewayConnection.cs) — Similar gateway abstraction pattern
- [ConsoleGatewayConnection](../src/Coven.Chat.Console/ConsoleGatewayConnection.cs) — Similar gateway pattern (stream-based)
- [Discord.Net Documentation](https://discordnet.dev/) — Underlying library documentation
