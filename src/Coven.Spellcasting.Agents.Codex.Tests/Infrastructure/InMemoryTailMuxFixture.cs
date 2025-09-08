using Coven.Spellcasting.Agents.Codex;
using Microsoft.Extensions.DependencyInjection;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

public sealed class InMemoryTailMuxFixture : ITailMuxFixture
{
    public ITestTailMux CreateMux(MuxArgs args)
    {
        var services = new ServiceCollection();
        services.AddTransient<ITailMux, InMemoryTailMux>();
        using var sp = services.BuildServiceProvider();
        return new MuxAdapter(sp.GetRequiredService<ITailMux>());
    }

    public async Task StimulateIncomingAsync(ITestTailMux mux, MuxArgs args, IEnumerable<string> lines)
    {
        var im = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
        foreach (var l in lines)
            await im.FeedAsync(new Line(l, DateTimeOffset.UtcNow));
    }
}
