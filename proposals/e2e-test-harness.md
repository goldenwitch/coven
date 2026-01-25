# Proposal: E2E Test Harness

## Status
Draft

## Summary

Introduce a black-box E2E test harness as a separate library (`Coven.Testing.Harness`) that virtualizes .NET process execution and OS hooks (console I/O, HTTP, Discord WebSockets) to enable testing complete samples and toys without external dependencies.

## Motivation

### Current State

The `Coven.E2E.Tests` project exists but contains **no source files**—it is a skeleton. All existing tests are unit/integration tests that exercise components in isolation:

| Test Project | Scope | External Dependencies |
|--------------|-------|----------------------|
| `Coven.Core.Tests` | Journal, blocks, rituals | None (uses `InMemoryScrivener`) |
| `Coven.Covenants.Tests` | Covenant routing | None |
| `Coven.Daemonology.Tests` | Daemon lifecycle | None (uses `TestDaemon`) |

### The Gap

The samples and toys represent the user-facing contract of Coven. They combine:

- **Console I/O** (stdin/stdout via `System.Console`)
- **Discord API** (WebSocket + REST via `DiscordSocketClient`)
- **OpenAI API** (HTTP via `OpenAIClient`)
- **File System** (journal persistence via `FileScrivener`)

Without E2E tests, we cannot verify that these complete applications work correctly after refactoring core abstractions.

### Samples/Toys Requiring Coverage

| Program | Console | Discord | OpenAI | FileSystem |
|---------|---------|---------|--------|------------|
| `01.DiscordAgent` | ❌ | ✅ | ✅ | ✅ |
| `02.DeclarativeDiscordAgent` | ❌ | ✅ | ✅ | ✅ |
| `Coven.Toys.ConsoleChat` | ✅ | ❌ | ❌ | ❌ |
| `Coven.Toys.ConsoleOpenAI` | ✅ | ❌ | ✅ | ❌ |
| `Coven.Toys.ConsoleOpenAIStreaming` | ✅ | ❌ | ✅ | ❌ |
| `Coven.Toys.DiscordChat` | ❌ | ✅ | ❌ | ❌ |
| `Coven.Toys.DiscordStreaming` | ❌ | ✅ | ❌ | ❌ |
| `Coven.Toys.FileScrivenerConsole` | ✅ | ❌ | ❌ | ✅ |

## Design

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Coven.Testing.Harness                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │ VirtualConsole│  │ VirtualOpenAI│  │ VirtualDiscord       │   │
│  │              │  │              │  │                      │   │
│  │ IConsoleIO   │  │ IOAIGateway  │  │ IDiscordGateway      │   │
│  │ ChannelPipes │  │ ScriptedResp │  │ ScriptedMessages     │   │
│  └──────────────┘  └──────────────┘  └──────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    TestHostBuilder                         │   │
│  │  - Wires virtual services into DI                         │   │
│  │  - Provides test assertions                               │   │
│  │  - Manages lifecycle                                       │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Virtualization Seams

The Coven architecture already provides key virtualization seams:

#### 1. OpenAI Gateway (Existing Interface)

```csharp
// src/Coven.Agents.OpenAI/IOpenAIGatewayConnection.cs
internal interface IOpenAIGatewayConnection
{
    Task ConnectAsync();
    Task SendAsync(OpenAIEfferent outgoing, CancellationToken cancellationToken);
}
```

**Issue**: This interface is `internal`. To enable external testing, we either:
- Make it `public`, or
- Add `[InternalsVisibleTo("Coven.Testing.Harness")]`

**Recommendation**: Keep it internal and use `InternalsVisibleTo`. The interface is implementation-specific to the OpenAI adapter.

#### 2. Console Gateway (Needs Abstraction)

Currently, `ConsoleGatewayConnection` uses `System.Console` directly:

```csharp
// Current implementation
line = await SysConsole.In.ReadLineAsync(ct).ConfigureAwait(false);
// ...
SysConsole.WriteLine(text);
```

**Proposed Abstraction**:

```csharp
// src/Coven.Chat.Console/IConsoleIO.cs
public interface IConsoleIO
{
    TextReader In { get; }
    TextWriter Out { get; }
    TextWriter Error { get; }
    
    // Optional: higher-level async operations
    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);
    ValueTask WriteLineAsync(string value, CancellationToken cancellationToken);
}

// Production implementation (default)
internal sealed class SystemConsoleIO : IConsoleIO
{
    public TextReader In => Console.In;
    public TextWriter Out => Console.Out;
    public TextWriter Error => Console.Error;
    
    public ValueTask<string?> ReadLineAsync(CancellationToken ct)
        => new(Console.In.ReadLineAsync(ct));
    
    public ValueTask WriteLineAsync(string value, CancellationToken ct)
    {
        Console.WriteLine(value);
        return ValueTask.CompletedTask;
    }
}
```

#### 3. Discord Gateway (Needs Abstraction — See Dependent Proposal)

Currently, `DiscordGatewayConnection` uses `DiscordSocketClient` directly with tight coupling to Discord.Net specifics:

- Uses `socketClient.MessageReceived` event (event-based, not interface method)
- Channel resolution uses cache-first + REST fallback: `socketClient.GetChannel()` then `socketClient.Rest.GetChannelAsync()`
- Message sending goes through `IMessageChannel.SendMessageAsync()` resolved from the client

**This abstraction requires a separate proposal.** See [Dependent Proposal: Discord Gateway Abstraction](#dependent-proposal-discord-gateway-abstraction) below.

The target interface shape:

```csharp
// src/Coven.Chat.Discord/IDiscordGateway.cs (pending dependent proposal)
public interface IDiscordGateway
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    
    IAsyncEnumerable<DiscordInboundMessage> InboundMessages { get; }
    Task SendMessageAsync(ulong channelId, string content, CancellationToken ct);
}

public sealed record DiscordInboundMessage(
    ulong ChannelId,
    string Author,
    string Content,
    string MessageId,
    DateTimeOffset Timestamp,
    bool IsBot);
```

### Test Harness Components

#### VirtualConsoleIO

The console virtualization requires bridging `Channel<string>` to the `TextReader`/`TextWriter` abstractions that `ConsoleGatewayConnection` uses via `SysConsole.In` and `SysConsole.Out`.

##### Channel Adapters

```csharp
/// <summary>
/// A TextReader that reads lines from a Channel. Returns null from ReadLineAsync
/// when the channel completes, signaling EOF to consumers like _stdinPump.
/// </summary>
internal sealed class ChannelTextReader : TextReader
{
    private readonly ChannelReader<string> _reader;

    public ChannelTextReader(ChannelReader<string> reader) => _reader = reader;

    public override string? ReadLine()
        => _reader.TryRead(out var line) ? line : null;

    public override async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        try
        {
            // WaitToReadAsync returns false when Complete() has been called
            // and all buffered items are consumed
            if (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (_reader.TryRead(out var line))
                    return line;
            }
            // Channel completed = EOF
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}

/// <summary>
/// A TextWriter that writes lines to a Channel for test capture.
/// </summary>
internal sealed class ChannelTextWriter : TextWriter
{
    private readonly ChannelWriter<string> _writer;
    private readonly StringBuilder _buffer = new();

    public ChannelTextWriter(ChannelWriter<string> writer) => _writer = writer;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
            Flush();
        else if (value != '\r')
            _buffer.Append(value);
    }

    public override void WriteLine(string? value)
    {
        _buffer.Append(value);
        Flush();
    }

    public override void Flush()
    {
        if (_buffer.Length > 0)
        {
            _writer.TryWrite(_buffer.ToString());
            _buffer.Clear();
        }
    }

    public override async Task WriteLineAsync(string? value)
    {
        var line = _buffer.Append(value).ToString();
        _buffer.Clear();
        await _writer.WriteAsync(line).ConfigureAwait(false);
    }
}
```

##### VirtualConsoleIO Implementation

```csharp
// src/Coven.Testing.Harness/VirtualConsoleIO.cs
public sealed class VirtualConsoleIO : IConsoleIO, IAsyncDisposable
{
    private readonly Channel<string> _inputChannel = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _outputChannel = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _errorChannel = Channel.CreateUnbounded<string>();

    public VirtualConsoleIO()
    {
        In = new ChannelTextReader(_inputChannel.Reader);
        Out = new ChannelTextWriter(_outputChannel.Writer);
        Error = new ChannelTextWriter(_errorChannel.Writer);
    }

    // IConsoleIO - wired to ConsoleGatewayConnection via DI
    public TextReader In { get; }
    public TextWriter Out { get; }
    public TextWriter Error { get; }

    // === Test Input API ===

    /// <summary>
    /// Sends a line of input to the console. The line becomes available
    /// to the _stdinPump's ReadLineAsync call.
    /// </summary>
    public ValueTask SendInputAsync(string line, CancellationToken ct = default)
        => _inputChannel.Writer.WriteAsync(line, ct);

    /// <summary>
    /// Signals EOF on stdin. After this call, ReadLineAsync will return null,
    /// causing _stdinPump to break out of its loop cooperatively.
    /// </summary>
    public void CompleteInput()
        => _inputChannel.Writer.Complete();

    // === Test Output API ===

    /// <summary>
    /// Waits for and returns the next line written to stdout.
    /// Throws TimeoutException if no output arrives within the timeout.
    /// </summary>
    public async ValueTask<string> WaitForOutputAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await _outputChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"No output received within {timeout}");
        }
    }

    /// <summary>
    /// Collects exactly <paramref name="count"/> output lines.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> CollectOutputAsync(
        int count, TimeSpan timeout, CancellationToken ct = default)
    {
        var lines = new List<string>(count);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        for (int i = 0; i < count; i++)
            lines.Add(await _outputChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false));

        return lines;
    }

    /// <summary>
    /// Drains all currently buffered output without waiting.
    /// </summary>
    public IReadOnlyList<string> DrainOutput()
    {
        var lines = new List<string>();
        while (_outputChannel.Reader.TryRead(out var line))
            lines.Add(line);
        return lines;
    }

    public ValueTask DisposeAsync()
    {
        _inputChannel.Writer.TryComplete();
        _outputChannel.Writer.TryComplete();
        _errorChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
```

##### EOF Signaling Flow

When a test calls `CompleteInput()`:

1. `_inputChannel.Writer.Complete()` marks the channel as completed
2. The next `WaitToReadAsync()` call in `ChannelTextReader` returns `false`
3. `ReadLineAsync()` returns `null`
4. `ConsoleGatewayConnection._stdinPump` sees `null` and breaks its loop
5. The pump task completes, allowing clean shutdown via `DisposeAsync()`

This matches the real stdin behavior where EOF (Ctrl+D on Unix, Ctrl+Z on Windows) causes `ReadLineAsync` to return `null`.

#### VirtualOpenAIGateway

The virtual gateway must participate in Coven's response flow correctly. Real gateway implementations don't return responses through the `SendAsync` method—they inject responses into the system via a keyed internal scrivener. The virtual gateway must do the same.

**Response Flow:**

```
Test Setup                    VirtualOpenAIGateway                Internal Scrivener
    |                                  |                                  |
    |-- EnqueueResponse() ------------>|                                  |
    |                                  |                                  |
    |                        SendAsync(efferent)                          |
    |                                  |                                  |
    |                                  |-- WriteAsync(chunk1) ----------->|
    |                                  |-- WriteAsync(chunk2) ----------->|
    |                                  |-- WriteAsync(...) -------------->|
    |                                  |-- WriteAsync(StreamCompleted) -->|
    |                                  |                                  |
                                       |                     Coven processes entries
```

**Implementation:**

```csharp
public sealed class VirtualOpenAIGateway : IOpenAIGatewayConnection
{
    private readonly IScrivener<OpenAIEntry> _internalScrivener;
    private readonly Queue<IScriptedResponse> _responses = new();
    private readonly List<OpenAIEfferent> _sentMessages = new();
    
    public VirtualOpenAIGateway(
        [FromKeyedServices("Coven.InternalOpenAIScrivener")] IScrivener<OpenAIEntry> scrivener)
    {
        _internalScrivener = scrivener;
    }
    
    // Test setup API
    public void EnqueueResponse(string content, string? model = null)
        => _responses.Enqueue(new ScriptedCompleteResponse(content, model ?? "gpt-4o"));
    
    public void EnqueueStreamingResponse(IEnumerable<string> chunks, string? model = null)
        => _responses.Enqueue(new ScriptedStreamingResponse(chunks.ToList(), model ?? "gpt-4o"));
    
    public void EnqueueStreamingResponseWithThoughts(
        IEnumerable<string> thoughtChunks, 
        IEnumerable<string> responseChunks, 
        string? model = null)
        => _responses.Enqueue(new ScriptedStreamingWithThoughtsResponse(
            thoughtChunks.ToList(), responseChunks.ToList(), model ?? "gpt-4o"));
    
    // Assertions
    public IReadOnlyList<OpenAIEfferent> SentMessages => _sentMessages;
    
    // IOpenAIGatewayConnection
    public Task ConnectAsync() => Task.CompletedTask;
    
    public async Task SendAsync(OpenAIEfferent outgoing, CancellationToken ct)
    {
        _sentMessages.Add(outgoing);
        
        if (!_responses.TryDequeue(out var response))
            throw new InvalidOperationException(
                $"No scripted response available for message: {outgoing.Text[..Math.Min(50, outgoing.Text.Length)]}...");
        
        string responseId = Guid.NewGuid().ToString("N");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        
        await response.EmitAsync(_internalScrivener, responseId, timestamp, ct);
    }
}
```

**Scripted Response Types:**

```csharp
internal interface IScriptedResponse
{
    string Model { get; }
    Task EmitAsync(IScrivener<OpenAIEntry> scrivener, string responseId, DateTimeOffset timestamp, CancellationToken ct);
}

internal sealed record ScriptedCompleteResponse(string Content, string Model) : IScriptedResponse
{
    public async Task EmitAsync(IScrivener<OpenAIEntry> scrivener, string responseId, DateTimeOffset timestamp, CancellationToken ct)
    {
        await scrivener.WriteAsync(new OpenAIAfferentChunk(
            Sender: "openai",
            Text: Content,
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: Model), ct);
        
        await scrivener.WriteAsync(new OpenAIStreamCompleted(
            Sender: "openai",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: Model), ct);
    }
}

internal sealed record ScriptedStreamingResponse(IReadOnlyList<string> Chunks, string Model) : IScriptedResponse
{
    public async Task EmitAsync(IScrivener<OpenAIEntry> scrivener, string responseId, DateTimeOffset timestamp, CancellationToken ct)
    {
        foreach (var chunk in Chunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentChunk(
                Sender: "openai",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: Model), ct);
        }
        
        await scrivener.WriteAsync(new OpenAIStreamCompleted(
            Sender: "openai",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: Model), ct);
    }
}

internal sealed record ScriptedStreamingWithThoughtsResponse(
    IReadOnlyList<string> ThoughtChunks, 
    IReadOnlyList<string> ResponseChunks, 
    string Model) : IScriptedResponse
{
    public async Task EmitAsync(IScrivener<OpenAIEntry> scrivener, string responseId, DateTimeOffset timestamp, CancellationToken ct)
    {
        // Emit thought chunks first (reasoning summary)
        foreach (var thought in ThoughtChunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentThoughtChunk(
                Sender: "openai",
                Text: thought,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: Model), ct);
        }
        
        // Then emit response chunks
        foreach (var chunk in ResponseChunks)
        {
            await scrivener.WriteAsync(new OpenAIAfferentChunk(
                Sender: "openai",
                Text: chunk,
                ResponseId: responseId,
                Timestamp: timestamp,
                Model: Model), ct);
        }
        
        await scrivener.WriteAsync(new OpenAIStreamCompleted(
            Sender: "openai",
            ResponseId: responseId,
            Timestamp: timestamp,
            Model: Model), ct);
    }
}
```

**Key Points:**

1. **Keyed scrivener injection**: The gateway receives `IScrivener<OpenAIEntry>` via `[FromKeyedServices("Coven.InternalOpenAIScrivener")]`—the same pattern as production gateways
2. **Write, don't return**: Responses flow through `IScrivener.WriteAsync()`, not through method return values
3. **Entry types matter**: Use `OpenAIAfferentChunk` for response text, `OpenAIAfferentThoughtChunk` for reasoning/thinking, and `OpenAIStreamCompleted` to signal end-of-response
4. **Response metadata**: Each entry carries `ResponseId`, `Timestamp`, and `Model` for proper correlation

#### VirtualDiscordGateway

> **Note:** This implementation is blocked on the [Discord Gateway Abstraction](#dependent-proposal-discord-gateway-abstraction) dependent proposal. The code below represents the target design once that abstraction exists.

```csharp
// src/Coven.Testing.Harness/VirtualDiscordGateway.cs
public sealed class VirtualDiscordGateway : IDiscordGateway
{
    private readonly Channel<DiscordInboundMessage> _inbound = Channel.CreateUnbounded<DiscordInboundMessage>();
    private readonly List<(ulong ChannelId, string Content)> _sent = new();
    private readonly TaskCompletionSource _readyTcs = new();
    
    // Test Setup API
    public async Task SimulateMessageAsync(
        ulong channelId, 
        string author, 
        string content,
        string? messageId = null,
        DateTimeOffset? timestamp = null,
        bool isBot = false)
    {
        await _inbound.Writer.WriteAsync(new DiscordInboundMessage(
            channelId,
            author,
            content,
            messageId ?? Guid.NewGuid().ToString("N"),
            timestamp ?? DateTimeOffset.UtcNow,
            isBot));
    }
    
    public void CompleteInbound() => _inbound.Writer.Complete();
    
    // Assertions
    public IReadOnlyList<(ulong ChannelId, string Content)> SentMessages => _sent;
    
    // Internal: wait for "ready" state (simulates guild availability)
    internal Task WaitForReadyAsync(CancellationToken ct)
    {
        _readyTcs.TrySetResult();
        return Task.CompletedTask;
    }
    
    // IDiscordGateway
    public Task ConnectAsync(CancellationToken ct)
    {
        _readyTcs.TrySetResult();
        return Task.CompletedTask;
    }
    
    public Task DisconnectAsync() => Task.CompletedTask;
    
    public IAsyncEnumerable<DiscordInboundMessage> InboundMessages 
        => _inbound.Reader.ReadAllAsync();
    
    public Task SendMessageAsync(ulong channelId, string content, CancellationToken ct)
    {
        _sent.Add((channelId, content));
        return Task.CompletedTask;
    }
}
```

#### E2ETestHost

```csharp
// src/Coven.Testing.Harness/E2ETestHost.cs
public sealed class E2ETestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly VirtualConsoleIO? _console;
    private readonly VirtualOpenAIGateway? _openAI;
    private readonly VirtualDiscordGateway? _discord;
    
    public ICoven Coven => _host.Services.GetRequiredService<ICoven>();
    public VirtualConsoleIO Console => _console ?? throw new InvalidOperationException("Console not configured");
    public VirtualOpenAIGateway OpenAI => _openAI ?? throw new InvalidOperationException("OpenAI not configured");
    public VirtualDiscordGateway Discord => _discord ?? throw new InvalidOperationException("Discord not configured");
    
    public async Task StartAsync(CancellationToken ct = default)
    {
        await _host.StartAsync(ct);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
```

#### E2ETestHostBuilder

```csharp
// src/Coven.Testing.Harness/E2ETestHostBuilder.cs
public sealed class E2ETestHostBuilder
{
    private readonly HostApplicationBuilder _builder;
    private bool _useVirtualConsole;
    private bool _useVirtualOpenAI;
    private bool _useVirtualDiscord;
    private string? _tempDataDirectory;
    
    public E2ETestHostBuilder()
    {
        _builder = Host.CreateApplicationBuilder();
        _tempDataDirectory = Path.Combine(Path.GetTempPath(), $"coven-e2e-{Guid.NewGuid():N}");
    }
    
    public E2ETestHostBuilder WithVirtualConsole()
    {
        _useVirtualConsole = true;
        return this;
    }
    
    public E2ETestHostBuilder WithVirtualOpenAI()
    {
        _useVirtualOpenAI = true;
        return this;
    }
    
    public E2ETestHostBuilder WithVirtualDiscord()
    {
        _useVirtualDiscord = true;
        return this;
    }
    
    public E2ETestHostBuilder ConfigureCoven(Action<CovenServiceBuilder> configure)
    {
        _builder.Services.BuildCoven(configure);
        return this;
    }
    
    public E2ETestHost Build()
    {
        // Replace real implementations with virtual ones
        if (_useVirtualConsole)
        {
            var virtualConsole = new VirtualConsoleIO();
            _builder.Services.AddSingleton<IConsoleIO>(virtualConsole);
        }
        
        if (_useVirtualOpenAI)
        {
            _builder.Services.AddScoped<IOpenAIGatewayConnection, VirtualOpenAIGateway>();
        }
        
        if (_useVirtualDiscord)
        {
            _builder.Services.AddScoped<IDiscordGatewayConnection, VirtualDiscordGateway>();
        }
        
        return new E2ETestHost(_builder.Build(), /* virtual services */);
    }
}
```

### Test Lifecycle Coordination

#### Readiness Definitions

Each adapter type signals "ready to receive input" differently:

| Adapter | Ready Signal | Observable Via |
|---------|--------------|----------------|
| `ConsoleChatDaemon` | `_stdinPump` task started | `DaemonEvent.Status == Running` |
| `DiscordChatDaemon` | WebSocket connected, guild available | `DaemonEvent.Status == Running` |
| `OpenAIAgentDaemon` | HTTP client initialized | `DaemonEvent.Status == Running` (immediate) |

All daemons write status to `IScrivener<DaemonEvent>` and expose `WaitFor(DaemonStatus)`. This is the unified readiness primitive.

#### E2ETestHost.StartAsync()

```csharp
public async Task StartAsync(CancellationToken ct = default)
{
    using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    startupCts.CancelAfter(_startupTimeout); // Default: 30s
    
    try
    {
        // 1. Start the underlying host (triggers daemon Start())
        await _host.StartAsync(startupCts.Token);
        
        // 2. Wait for all registered daemons to reach Running status
        var daemons = _host.Services.GetServices<IDaemon>();
        var readinessTasks = daemons.Select(d => 
            d.WaitFor(DaemonStatus.Running, startupCts.Token));
        
        await Task.WhenAll(readinessTasks);
        
        // 3. Allow virtual gateways to stabilize (e.g., Discord "ready" event)
        if (_discord is not null)
            await _discord.WaitForReadyAsync(startupCts.Token);
    }
    catch (OperationCanceledException) when (startupCts.IsCancellationRequested && !ct.IsCancellationRequested)
    {
        throw new TimeoutException(
            $"E2E host failed to reach ready state within {_startupTimeout}. " +
            $"Daemon states: {FormatDaemonStates()}");
    }
}
```

#### E2ETestHost.DisposeAsync()

```csharp
public async ValueTask DisposeAsync()
{
    using var shutdownCts = new CancellationTokenSource(_shutdownTimeout); // Default: 10s
    
    try
    {
        // 1. Signal input completion to unblock stdin pumps
        _console?.CompleteInput();
        _discord?.CompleteInbound();
        
        // 2. Request graceful stop (triggers daemon cancellation tokens)
        await _host.StopAsync(shutdownCts.Token);
        
        // 3. Await session disposal (pump task completion)
        //    Sessions are IAsyncDisposable and hold sessionToken
        //    StopAsync triggers token cancellation → pumps exit → sessions dispose
        
        // 4. Collect any pump exceptions for diagnostics
        if (_pumpExceptions.Count > 0)
        {
            throw new AggregateException(
                "One or more background pumps threw exceptions during test execution",
                _pumpExceptions);
        }
    }
    catch (OperationCanceledException) when (shutdownCts.IsCancellationRequested)
    {
        // Forced shutdown—pumps may be deadlocked
        _host.Dispose(); // Synchronous fallback
        throw new TimeoutException(
            $"E2E host shutdown timed out after {_shutdownTimeout}. " +
            "Possible deadlock in message pump.");
    }
    finally
    {
        _host.Dispose();
    }
}
```

#### Timeout and Deadlock Protection

```csharp
public sealed class E2ETestHost
{
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _defaultAssertionTimeout = TimeSpan.FromSeconds(5);
    
    // All public wait methods enforce timeout
    public async Task<string> WaitForOutputAsync(TimeSpan? timeout = null)
    {
        timeout ??= _defaultAssertionTimeout;
        using var cts = new CancellationTokenSource(timeout.Value);
        
        try
        {
            return await _console!.WaitForOutputAsync(timeout.Value);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No output received within {timeout}. " +
                $"Pending input queue: {_console.PendingInputCount}");
        }
    }
}
```

#### Pump Exception Handling

Background pumps (stdin reader, Discord message receiver) run in fire-and-forget tasks. Exceptions must be captured for test diagnostics:

```csharp
public sealed class VirtualConsoleIO
{
    private readonly ConcurrentQueue<Exception> _pumpExceptions = new();
    
    internal void ReportPumpException(Exception ex) => _pumpExceptions.Enqueue(ex);
    
    internal IReadOnlyCollection<Exception> PumpExceptions => _pumpExceptions.ToArray();
}

// In session pump loops:
private async Task RunStdinPumpAsync(CancellationToken ct)
{
    try
    {
        await foreach (var line in _consoleIO.ReadLinesAsync(ct))
        {
            await ProcessInputAsync(line, ct);
        }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // Normal shutdown—ignore
    }
    catch (Exception ex)
    {
        _consoleIO.ReportPumpException(ex);
        throw; // Re-throw to mark task as faulted
    }
}
```

The `E2ETestHost.DisposeAsync()` aggregates these exceptions and throws if any pump failed, ensuring test failures surface even for background errors.

### Assertion Capabilities

E2E tests require assertions across several dimensions: verifying output content, checking ordering of events, validating journal state, and confirming nothing unexpected happened.

#### Assertion Categories

| Category | Purpose | Primary API |
|----------|---------|-------------|
| **Output Assertions** | Verify expected console/Discord output appeared | `WaitForOutputAsync`, `CollectOutputAsync` |
| **Journal Inspection** | Read raw journal entries for detailed verification | `E2ETestHost.Journals.*` |
| **Ordering Assertions** | Verify events occurred in expected sequence | `AssertOrderedAsync` |
| **Negative Assertions** | Verify nothing else happened within a timeout | `AssertQuietAsync` |
| **Request Verification** | Verify outbound requests (OpenAI prompts, Discord messages) | `VirtualOpenAIGateway.SentMessages` |

#### Journal Exposure

```csharp
public sealed partial class E2ETestHost
{
    /// <summary>
    /// Provides access to all registered journals for test inspection.
    /// </summary>
    public JournalAccessor Journals { get; }
}

public sealed class JournalAccessor
{
    private readonly IServiceProvider _services;
    
    public IScrivener<ChatEntry> Chat 
        => _services.GetRequiredService<IScrivener<ChatEntry>>();
    
    public IScrivener<OpenAIEntry> OpenAI 
        => _services.GetRequiredService<IScrivener<OpenAIEntry>>();
    
    public IScrivener<ConsoleEntry> Console 
        => _services.GetRequiredService<IScrivener<ConsoleEntry>>();
    
    public IScrivener<DiscordEntry> Discord 
        => _services.GetRequiredService<IScrivener<DiscordEntry>>();
    
    public IScrivener<TEntry> Get<TEntry>() where TEntry : Entry
        => _services.GetRequiredService<IScrivener<TEntry>>();
}
```

#### Timeout Strategy

| Timeout Level | Default | Configuration |
|---------------|---------|---------------|
| **Global** | 30 seconds | `E2ETestHostBuilder.WithTimeout(TimeSpan)` |
| **Per-Assertion** | Inherits global | Override via method parameter |

#### Assertion Helper Methods

```csharp
public static class E2ETestHostAssertions
{
    /// <summary>
    /// Waits for a specific output line matching the predicate.
    /// </summary>
    public static async Task<string> WaitForOutputAsync(
        this VirtualConsoleIO console,
        Func<string, bool> predicate,
        TimeSpan? timeout = null,
        string? because = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        using var cts = new CancellationTokenSource(timeout.Value);
        
        while (true)
        {
            string line = await console.WaitForOutputAsync(timeout.Value);
            if (predicate(line))
                return line;
        }
    }
    
    /// <summary>
    /// Asserts that journal entries appear in the expected order.
    /// </summary>
    public static async Task AssertOrderedAsync<TEntry>(
        this IScrivener<TEntry> journal,
        params Func<TEntry, bool>[] predicates)
        where TEntry : Entry
    {
        var entries = await journal.ReadAsync();
        int predicateIndex = 0;
        
        foreach (var entry in entries)
        {
            if (predicateIndex < predicates.Length && predicates[predicateIndex](entry))
                predicateIndex++;
        }
        
        if (predicateIndex < predicates.Length)
            throw new XunitException(
                $"Expected {predicates.Length} ordered matches, found {predicateIndex}");
    }
    
    /// <summary>
    /// Asserts that no additional output arrives within the timeout period.
    /// Use this to verify the system has quiesced after expected output.
    /// </summary>
    public static async Task AssertQuietAsync(
        this VirtualConsoleIO console,
        TimeSpan timeout)
    {
        try
        {
            string unexpected = await console.WaitForOutputAsync(timeout);
            throw new XunitException(
                $"Expected no output within {timeout}, but received: {unexpected}");
        }
        catch (TimeoutException)
        {
            // Expected - no output within timeout means success
        }
    }
}
```

#### Usage Examples

```csharp
[Fact]
public async Task Chat_JournalRecordsConversationOrder()
{
    await using var host = CreateChatHost();
    await host.StartAsync();
    
    await host.Console.SendInputAsync("Hello");
    await host.Console.WaitForOutputAsync(TimeSpan.FromSeconds(5));
    
    // Verify journal entries are in correct order
    await host.Journals.Chat.AssertOrderedAsync(
        e => e is ChatAfferent { Content: "Hello" },
        e => e is ChatEfferent { Sender: "bot" }
    );
}

[Fact]
public async Task Agent_DoesNotRespondToEmptyInput()
{
    await using var host = CreateAgentHost();
    await host.StartAsync();
    
    await host.Console.SendInputAsync("");
    
    // Verify no response is generated
    await host.Console.AssertQuietAsync(TimeSpan.FromSeconds(2));
}
```

### File System Isolation

#### Strategy: In-Memory Replacement

For E2E tests, **replace `FileScrivener` with `InMemoryScrivener`** rather than redirecting paths to temporary directories:

| Approach | Pros | Cons |
|----------|------|------|
| **Replace with InMemoryScrivener** | No cleanup needed, parallelism-safe by design, faster | Cannot test actual file I/O behavior |
| Redirect to temp paths | Tests real file operations | Requires cleanup, path collision risk in parallel tests |

Since E2E tests verify *application behavior* (message routing, daemon lifecycle, covenant adherence) rather than *file I/O correctness*, the replacement strategy is preferred. The `FileScrivener` itself is unit-tested separately.

#### Builder Configuration

```csharp
public sealed class E2ETestHostBuilder
{
    private readonly List<Type> _inMemoryScrivenerTypes = new();
    
    public E2ETestHostBuilder WithInMemoryScrivener<TEntry>() where TEntry : notnull
    {
        _inMemoryScrivenerTypes.Add(typeof(TEntry));
        return this;
    }
    
    public E2ETestHost Build()
    {
        // Replace file scriveners with in-memory equivalents
        foreach (var entryType in _inMemoryScrivenerTypes)
        {
            ReplaceFileScrivenerWithInMemory(_builder.Services, entryType);
        }
        
        return new E2ETestHost(_builder.Build(), /* ... */);
    }
    
    private static void ReplaceFileScrivenerWithInMemory(IServiceCollection services, Type entryType)
    {
        var scrivenerInterface = typeof(IScrivener<>).MakeGenericType(entryType);
        var descriptor = services.FirstOrDefault(d => d.ServiceType == scrivenerInterface);
        if (descriptor != null)
            services.Remove(descriptor);
        
        var inMemoryType = typeof(InMemoryScrivener<>).MakeGenericType(entryType);
        services.AddScoped(scrivenerInterface, inMemoryType);
    }
}
```

#### Test Parallelism

In-memory replacement guarantees parallelism safety—each test creates its own `E2ETestHost` with an isolated DI container.

### Streaming Test Model

Streaming flows involve layered buffering that transforms raw API chunks into user-visible outputs. Tests must understand where virtualization occurs and how policies affect observable behavior.

#### Layered Buffering Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          INBOUND (API → User)                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   VirtualOpenAIGateway          StreamWindowingDaemon           Chat/UI     │
│   ┌──────────────────┐         ┌───────────────────┐        ┌───────────┐  │
│   │ Scripted chunks  │ ──────► │ IWindowPolicy     │ ─────► │ Aggregated│  │
│   │ (raw API level)  │         │ aggregates chunks │        │ outputs   │  │
│   │                  │         │ into batches      │        │           │  │
│   │ "Hel" "lo " "wo" │         │                   │        │ "Hello    │  │
│   │ "rld" "!"        │         │ Emits on policy   │        │  world!"  │  │
│   └──────────────────┘         │ match (paragraph, │        └───────────┘  │
│                                │ length, etc.)     │                       │
│                                └───────────────────┘                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key insight**: `VirtualOpenAIGateway` scripts RAW chunks at the API boundary—before any windowing occurs. This means:

- Test input: Individual token-level chunks (e.g., `"Hel"`, `"lo "`, `"wo"`, `"rld"`, `"!"`)
- Observable output: Policy-aggregated messages (e.g., `"Hello world!"`)

#### Making Tests Deterministic

Tests have three strategies:

**Strategy 1: Fixed Test Policies** — Override the production policy with a deterministic test-specific policy:

```csharp
var host = new E2ETestHostBuilder()
    .UseVirtualOpenAI()
    .ConfigureServices(services =>
    {
        // Replace windowing with "emit immediately"
        services.AddScoped<IWindowPolicy<AgentAfferentChunk>>(_ =>
            new ImmediateWindowPolicy<AgentAfferentChunk>());
    })
    .Build();
```

**Strategy 2: Policy-Aware Assertions** — Assert on aggregated content without assuming chunk boundaries:

```csharp
var outputs = await console.CollectAllOutputAsync(timeout);
var combined = string.Join("", outputs);
combined.Should().Contain("The answer is 42");
```

**Strategy 3: Scripted Chunk Boundaries** — Script chunks that align with policy boundaries for predictable emissions:

```csharp
// With paragraph-first policy, include explicit paragraph boundaries
gateway.EnqueueStreamingResponse(new[] 
{
    "First paragraph.\n\n",  // Triggers emission
    "Second paragraph."      // Emits on completion
});
```

### Covenant Configuration in Tests

E2E tests must configure valid covenants. The `BuildCoven()` method validates routing rules at build time—all produced types must be routed or marked terminal, and duplicate routes are prohibited. **The harness does not bypass validation**; tests exercise the same covenant constraints as production code.

#### Avoid Duplicating Sample Configuration

The example tests duplicate covenant setup from sample `Program.cs` files. When samples change, duplicated test configuration becomes stale. Instead, samples should expose their covenant configuration as reusable methods.

#### Recommended Pattern

```csharp
// src/toys/Coven.Toys.ConsoleChat/Program.cs
public static class ConsoleChatConfiguration
{
    public static void ConfigureCovenant(CovenServiceBuilder coven)
    {
        BranchManifest chat = coven.UseConsole(new ConsoleClientConfig { InputSender = "user" });
        coven.Covenant().Connect(chat).Routes(c =>
        {
            c.Route<ChatAfferent, ChatEfferentDraft>((msg, _) => 
                new ChatEfferentDraft("bot", msg.Content));
        });
    }
}

// Test using shared configuration
[Fact]
public async Task ConsoleChat_EchosUserInput()
{
    await using var host = new E2ETestHostBuilder()
        .WithVirtualConsole()
        .ConfigureCoven(ConsoleChatConfiguration.ConfigureCovenant)
        .Build();

    await host.StartAsync();
    // ...
}
```

This ensures tests always validate the actual sample covenant configuration.

### Dependent Proposal: Discord Gateway Abstraction

This proposal assumes testable gateway abstractions that do not yet exist. The Discord integration currently couples directly to `DiscordSocketClient`, making it impossible to substitute a fake gateway for E2E testing without a separate refactoring effort.

**Why separate:** The gateway abstraction is independently valuable (enables mocking for unit tests, alternative Discord libraries, rate-limit simulation) and has its own design decisions orthogonal to E2E test infrastructure.

**That proposal must address:**

1. **`IDiscordGateway` extraction** — Abstract the `DiscordSocketClient` dependency behind an interface that exposes inbound messages and outbound send capability
2. **Event-to-stream adaptation** — Convert Discord.Net's `MessageReceived` event model to `IAsyncEnumerable<DiscordInboundMessage>`, preserving `MessageId`, `Timestamp`, and channel metadata
3. **Bot self-filtering** — Decide where bot-message filtering lives (gateway adapter vs. consumer)
4. **Channel resolution** — Abstract the cache-first + REST fallback pattern currently baked into the concrete client
5. **Outbound simplification** — Expose `SendMessageAsync(ulong channelId, string content)` rather than requiring `IMessageChannel` resolution at the call site

**Blocking relationship:** E2E tests for Discord-based covenants are blocked until this abstraction exists. The `VirtualDiscordGateway` described in this proposal cannot be implemented against the current concrete coupling.

### Example Tests

#### ConsoleChat Echo Test

```csharp
[Fact]
public async Task ConsoleChat_EchosUserInput()
{
    // Arrange
    await using var host = new E2ETestHostBuilder()
        .WithVirtualConsole()
        .ConfigureCoven(coven =>
        {
            // Same setup as Coven.Toys.ConsoleChat/Program.cs
            BranchManifest chat = coven.UseConsole(new ConsoleClientConfig { InputSender = "user" });
            coven.Covenant().Connect(chat).Routes(c =>
            {
                c.Route<ChatAfferent, ChatEfferentDraft>((msg, _) => 
                    new ChatEfferentDraft("bot", msg.Content));
            });
        })
        .Build();
    
    await host.StartAsync();
    
    // Act
    await host.Console.SendInputAsync("Hello, world!");
    
    // Assert
    var output = await host.Console.WaitForOutputAsync(TimeSpan.FromSeconds(5));
    Assert.Equal("[bot]: Hello, world!", output);
}
```

#### ConsoleOpenAI Agent Test

```csharp
[Fact]
public async Task ConsoleOpenAI_RespondsWithAgentMessage()
{
    // Arrange
    await using var host = new E2ETestHostBuilder()
        .WithVirtualConsole()
        .WithVirtualOpenAI()
        .ConfigureCoven(coven =>
        {
            // Same setup as Coven.Toys.ConsoleOpenAI/Program.cs
            BranchManifest chat = coven.UseConsole(new ConsoleClientConfig());
            BranchManifest agent = coven.UseOpenAIAgent(new OpenAIClientConfig 
            { 
                Model = "gpt-4o" 
            });
            
            coven.Covenant().Connect(chat).Connect(agent).Routes(c =>
            {
                c.Route<ChatAfferent, AgentPrompt>((msg, _) => new AgentPrompt(msg.Content));
                c.Route<AgentResponse, ChatEfferentDraft>((r, _) => 
                    new ChatEfferentDraft("assistant", r.Content));
                c.Terminal<AgentThought>();
            });
        })
        .Build();
    
    // Script the AI response
    host.OpenAI.EnqueueResponse("I am a helpful assistant.");
    
    await host.StartAsync();
    
    // Act
    await host.Console.SendInputAsync("Who are you?");
    
    // Assert
    var output = await host.Console.WaitForOutputAsync(TimeSpan.FromSeconds(5));
    Assert.Contains("helpful assistant", output);
    
    // Verify the prompt was sent correctly
    Assert.Single(host.OpenAI.SentMessages);
    Assert.Equal("Who are you?", host.OpenAI.SentMessages[0].Content);
}
```

#### Streaming Test

```csharp
[Fact]
public async Task ConsoleOpenAIStreaming_StreamsResponseChunks()
{
    // Arrange
    await using var host = new E2ETestHostBuilder()
        .WithVirtualConsole()
        .WithVirtualOpenAI()
        .ConfigureCoven(coven =>
        {
            // Same setup as Coven.Toys.ConsoleOpenAIStreaming/Program.cs
            BranchManifest chat = coven.UseConsole(new ConsoleClientConfig());
            BranchManifest agent = coven.UseOpenAIAgent(new OpenAIClientConfig { Model = "gpt-4o" }, 
                streaming: true);
            
            // ... windowing and shatter policies
        })
        .Build();
    
    // Script streaming response
    host.OpenAI.EnqueueStreamingResponse(["Hello", " ", "World", "!"]);
    
    await host.StartAsync();
    
    // Act
    await host.Console.SendInputAsync("Say hello");
    
    // Assert - chunks arrive as configured by windowing policy
    var outputs = await host.Console.CollectOutputAsync(count: 2, TimeSpan.FromSeconds(5));
    Assert.Equal("Hello ", outputs[0]);
    Assert.Equal("World!", outputs[1]);
}
```

### Project Structure

```
src/
├── Coven.Testing.Harness/
│   ├── Coven.Testing.Harness.csproj
│   ├── E2ETestHost.cs
│   ├── E2ETestHostBuilder.cs
│   ├── VirtualConsoleIO.cs
│   ├── VirtualOpenAIGateway.cs
│   ├── VirtualDiscordGateway.cs
│   ├── Assertions/
│   │   ├── ConsoleAssertions.cs
│   │   ├── JournalAssertions.cs
│   │   └── OpenAIAssertions.cs
│   └── Scripting/
│       ├── OpenAIScriptedResponse.cs
│       └── DiscordScriptedConversation.cs
│
├── Coven.E2E.Tests/
│   ├── Coven.E2E.Tests.csproj
│   ├── Toys/
│   │   ├── ConsoleChatTests.cs
│   │   ├── ConsoleOpenAITests.cs
│   │   ├── ConsoleOpenAIStreamingTests.cs
│   │   ├── DiscordChatTests.cs
│   │   ├── DiscordStreamingTests.cs
│   │   └── FileScrivenerConsoleTests.cs
│   └── Samples/
│       ├── DiscordAgentTests.cs
│       └── DeclarativeDiscordAgentTests.cs
```

## Required Changes to Core Libraries

### 1. Make `IOpenAIGatewayConnection` Accessible

```csharp
// src/Coven.Agents.OpenAI/Coven.Agents.OpenAI.csproj
<ItemGroup>
  <InternalsVisibleTo Include="Coven.Testing.Harness" />
</ItemGroup>
```

### 2. Introduce `IConsoleIO` Abstraction

Add to `Coven.Chat.Console`:

```csharp
// src/Coven.Chat.Console/IConsoleIO.cs
public interface IConsoleIO { /* as above */ }

// Update ConsoleGatewayConnection to use IConsoleIO instead of SysConsole
```

### 3. Introduce `IDiscordGateway` Abstraction

**Blocked on dependent proposal.** See [Dependent Proposal: Discord Gateway Abstraction](#dependent-proposal-discord-gateway-abstraction).

Add to `Coven.Chat.Discord`:

```csharp
// src/Coven.Chat.Discord/IDiscordGateway.cs
public interface IDiscordGateway { /* defined in dependent proposal */ }

// Update DiscordGatewayConnection to implement IDiscordGateway
// Update DiscordChatSession to depend on IDiscordGateway instead of DiscordSocketClient
```

### 4. Add `Coven.E2E.Tests` to Solution

```xml
<!-- src/Coven.sln -->
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Coven.E2E.Tests", "Coven.E2E.Tests\Coven.E2E.Tests.csproj", "{...}"
```

## Implementation Phases

```
                           ┌─────────────────────────────────┐
                           │  Dependent Proposal:            │
                           │  Discord Gateway Abstraction    │
                           └───────────────┬─────────────────┘
                                           │ blocks
                                           ▼
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Phase 1  │───►│ Phase 2  │───►│ Phase 3  │───►│ Phase 4  │───►│ Phase 5  │
│ Console  │    │ Harness  │    │ Console  │    │ Discord  │    │ Full     │
│ Abstrac. │    │ Library  │    │ Tests    │    │ Tests    │    │ Samples  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
                                                     ▲
                                                     │ requires
                                     Discord Gateway Abstraction
```

### Phase 1: Console Abstraction (Core Changes)

1. Add `IConsoleIO` abstraction to `Coven.Chat.Console`
2. Add `InternalsVisibleTo` for OpenAI gateway
3. Verify existing tests still pass

### Phase 2: Test Harness Library

1. Create `Coven.Testing.Harness` project
2. Implement `ChannelTextReader` and `ChannelTextWriter`
3. Implement `VirtualConsoleIO`
4. Implement `VirtualOpenAIGateway` with keyed scrivener pattern
5. Implement `E2ETestHostBuilder` and `E2ETestHost`
6. Implement `JournalAccessor` and assertion helpers

### Phase 3: Console Toy Tests

1. Implement `ConsoleChatTests` (simplest case)
2. Implement `ConsoleOpenAITests`
3. Implement `ConsoleOpenAIStreamingTests`
4. Implement `FileScrivenerConsoleTests`
5. Extract shared configuration methods from toys

### Phase 4: Discord Integration (Blocked)

**Requires:** Dependent Proposal: Discord Gateway Abstraction

1. Implement `IDiscordGateway` abstraction in `Coven.Chat.Discord`
2. Implement `VirtualDiscordGateway` in harness
3. Implement `DiscordChatTests`
4. Implement `DiscordStreamingTests`

### Phase 5: Full Sample Tests (Blocked)

**Requires:** Phase 4

1. Implement `DiscordAgentTests` (01.DiscordAgent)
2. Implement `DeclarativeDiscordAgentTests` (02.DeclarativeDiscordAgent)

## Alternatives Considered

### Subprocess Black-Box Testing

Instead of in-process virtualization, launch samples as actual processes and communicate via redirected stdin/stdout.

**Pros:**
- Tests actual deployment artifact
- No code changes required to samples
- True black-box

**Cons:**
- Slower (process startup overhead)
- Harder to debug
- Still need HTTP mocking for OpenAI/Discord (via environment config or proxy)
- Platform-specific behaviors

**Decision**: In-process virtualization preferred for:
- Speed (instant startup)
- Debuggability
- Determinism
- Simpler test setup

The subprocess approach could be added later as a "smoke test" layer that verifies the actual executables work.

### WebApplicationFactory Pattern

Similar to ASP.NET's `WebApplicationFactory<TStartup>`, create a console app factory.

**Decision**: The proposed `E2ETestHostBuilder` achieves the same goal but tailored to Coven's daemon-based architecture rather than HTTP request/response.

## Open Questions

1. **Should the harness be a separate repo?**
   
   The proposal places it under `src/Coven.Testing.Harness` for convenience. If external projects want to test Coven integrations, a separate package would help. Recommendation: Start in-repo, extract if needed.

2. **Should virtual services record timing information?**
   
   For testing streaming behaviors, it might be useful to record when chunks were emitted. Adds complexity but could help debug timing-sensitive tests.

3. ~~**How to handle non-deterministic daemon startup order?**~~
   
   **Resolved.** See [Test Lifecycle Coordination](#test-lifecycle-coordination). `E2ETestHost.StartAsync()` waits for all daemons to reach `Running` status before returning.

4. **Should `IOpenAITranscriptBuilder` be mockable?**
   
   The current design uses the real transcript builder. For tests that need to verify exact prompt construction, consider exposing this as another virtualization seam.
