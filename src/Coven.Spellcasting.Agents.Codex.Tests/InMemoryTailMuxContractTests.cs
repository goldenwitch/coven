using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

public sealed class InMemoryTailMux_ContractTests : TailMuxContract<InMemoryTailMuxFixture>
{
    public InMemoryTailMux_ContractTests(InMemoryTailMuxFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Write_Can_Be_Observed_From_Outgoing_Channel()
    {
        await using var mux = Fixture.CreateMux(new MuxArgs("unused"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var collected = new List<string>();
        var readerTask = Task.Run(async () =>
        {
            var underlying = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
            await foreach (var s in underlying.ReadWritesAsync(cts.Token)) collected.Add(s);
        });

        await mux.WriteLineAsync("alpha");
        await mux.WriteLineAsync("beta");

        await Task.Delay(100);
        cts.Cancel();
        await readerTask;

        Assert.Contains("alpha", collected);
        Assert.Contains("beta", collected);
    }
}

