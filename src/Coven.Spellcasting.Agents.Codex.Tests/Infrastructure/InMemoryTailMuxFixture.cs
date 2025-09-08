using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class InMemoryTailMuxFixture : ITailMuxFixture, IDisposable
{
    private readonly IHost _host;

    public InMemoryTailMuxFixture()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddTransient<ITailMux>(sp => new InMemoryTailMux());
            })
            .Build();
    }

    public ITestTailMux CreateMux()
    {
        var inner = _host.Services.GetRequiredService<ITailMux>();
        return new MuxAdapter(inner);
    }

    public async Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines)
    {
        var im = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
        foreach (var l in lines)
            await im.FeedAsync(new Line(l, DateTimeOffset.UtcNow));
    }

    public Task CreateBackingFileAsync(ITestTailMux mux) => Task.CompletedTask;
    public Task WaitUntilTailReadyAsync(ITestTailMux mux, CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose()
    {
        _host.Dispose();
    }
}
