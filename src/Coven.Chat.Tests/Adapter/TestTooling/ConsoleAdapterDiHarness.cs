namespace Coven.Chat.Tests.Adapter.TestTooling;

using Coven.Chat;
using Coven.Chat.Adapter;
using Coven.Chat.Adapter.Console;
using Coven.Chat.Adapter.Console.Di;
using Microsoft.Extensions.DependencyInjection;

public sealed class ConsoleAdapterDiHarness : IDisposable
{
    public FakeConsoleIO IO { get; private set; } = null!;
    public ServiceProvider Services { get; private set; } = null!;
    public IAdapterHost<ChatEntry> Host { get; private set; } = null!;
    public IAdapter<ChatEntry> Adapter { get; private set; } = null!;
    public IScrivener<ChatEntry> Scrivener { get; private set; } = null!;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _run;

    private ConsoleAdapterDiHarness(ServiceProvider sp, FakeConsoleIO io)
    {
        Services = sp;
        IO = io;
        Host = sp.GetRequiredService<IAdapterHost<ChatEntry>>();
        Adapter = sp.GetRequiredService<IAdapter<ChatEntry>>();
        Scrivener = sp.GetRequiredService<IScrivener<ChatEntry>>();
        _run = Host.RunAsync(Scrivener, Adapter, _cts.Token);
    }

    public Task HostTask => _run;

    public static ConsoleAdapterDiHarness Start(Action<ConsoleAdapterOptions>? configure = null)
    {
        var services = new ServiceCollection();
        var io = new FakeConsoleIO();
        services.AddSingleton<IConsoleIO>(io);
        services.AddConsoleChatAdapter(configure);
        var sp = services.BuildServiceProvider();
        return new ConsoleAdapterDiHarness(sp, io);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _run.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
        Services.Dispose();
    }
}
