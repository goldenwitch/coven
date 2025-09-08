using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

namespace Coven.Spellcasting.Agents.Codex.Tests;

/// <summary>
/// Contract test runner bound to the in-memory tail mux implementation.
/// Verifies the shared contract from <see cref="TailMuxContract{TFixture}"/> against <see cref="InMemoryTailMux"/>.
/// </summary>
public sealed class InMemoryTailMux_ContractTests : TailMuxContract<InMemoryTailMuxFixture>
{
    public InMemoryTailMux_ContractTests(InMemoryTailMuxFixture fixture) : base(fixture) { }

    /// <summary>
    /// Validates that writes sent through <see cref="ITailMux.WriteLineAsync"/> are observable
    /// from the in-memory outgoing channel, modeling the asymmetric write path.
    /// </summary>
    [Fact]
    public async Task Write_Can_Be_Observed_From_Outgoing_Channel()
    {
        await using var mux = Fixture.CreateMux();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var collected = new List<string>();
        var readerTask = Task.Run(async () =>
        {
            var underlying = (InMemoryTailMux)((MuxAdapter)mux).Underlying;
            await foreach (var s in underlying.ReadWritesAsync(cts.Token)) collected.Add(s);
        });

        await mux.WriteLineAsync("alpha");
        await mux.WriteLineAsync("beta");

        var ok = await TailMuxTestHelpers.WaitUntilAsync(() => collected.Count >= 2, TimeSpan.FromSeconds(2));
        Assert.True(ok, "Timed out waiting for writes to be observed");
        cts.Cancel();
        await readerTask;

        Assert.Contains("alpha", collected);
        Assert.Contains("beta", collected);
    }
}
