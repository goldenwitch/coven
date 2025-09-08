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
                services.AddTransient<Func<MuxArgs, ITailMux>>(sp => args => new InMemoryTailMux());
            })
            .Build();
    }

    public ITestTailMux CreateMux(MuxArgs args)
    {
        var factory = _host.Services.GetRequiredService<Func<MuxArgs, ITailMux>>();
        return new MuxAdapter(factory(args));
    }

    public async Task StimulateIncomingAsync(ITestTailMux mux, MuxArgs args, IEnumerable<string> lines)
    {
        var im = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
        foreach (var l in lines)
            await im.FeedAsync(new Line(l, DateTimeOffset.UtcNow));
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
