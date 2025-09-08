namespace Coven.Chat.Tests.Adapter;

using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console;
using Coven.Chat.Adapter.Console.Di;
using Coven.Chat.Tests.Adapter.TestTooling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ConsoleAdapterDiE2eTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IAdapterHost<ChatEntry> _host;
    private readonly IAdapter<ChatEntry> _adapter;
    private readonly IScrivener<ChatEntry> _scrivener;
    private readonly FakeConsoleIO _io;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _run;

    public ConsoleAdapterDiE2eTests()
    {
        var services = new ServiceCollection();

        // Override console IO with a fake before registering the adapter stack.
        _io = new FakeConsoleIO();
        services.AddSingleton<IConsoleIO>(_io);

        services.AddConsoleChatAdapter(o =>
        {
            o.InputSender = "cli";
            o.EchoUserInput = false;
        });

        _sp = services.BuildServiceProvider();
        _host = _sp.GetRequiredService<IAdapterHost<ChatEntry>>();
        _adapter = _sp.GetRequiredService<IAdapter<ChatEntry>>();
        _scrivener = _sp.GetRequiredService<IScrivener<ChatEntry>>();

        _run = _host.RunAsync(_scrivener, _adapter, _cts.Token);
    }

    [Fact]
    public async Task EndToEnd_Console_Input_And_Output()
    {
        // Ingress: simulate user typing
        _io.EnqueueInput("hello coven");

        // Scrivener should receive ChatThought
        var seenThought = await _scrivener.WaitForAsync(0, e => e is ChatThought t && t.Text == "hello coven");
        Assert.IsType<ChatThought>(seenThought.entry);

        // Egress: write a response into the journal, expect console output
        _ = await _scrivener.WriteAsync(new ChatResponse("assistant", "hi user"));

        var delivered = await TryDequeueOutputAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("hi user", delivered);
    }

    private async Task<string?> TryDequeueOutputAsync(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (_io.TryDequeueOutput(out var line)) return line;
            await Task.Delay(10);
        }
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _run.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
        _sp.Dispose();
    }
}

