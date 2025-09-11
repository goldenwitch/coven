// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents.Tests.Infrastructure;
using Xunit;

namespace Coven.Spellcasting.Agents.Tests;

/// <summary>
/// Contract test runner bound to the in-memory tail mux implementation.
/// Verifies the shared contract against <see cref="InMemoryTailMux"/> and validates write observation.
/// </summary>
public sealed class InMemoryTailMux_ContractTests : TailMuxContract<InMemoryTailMuxFixture>
{
    public InMemoryTailMux_ContractTests(InMemoryTailMuxFixture fixture) : base(fixture) { }

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

        var ok = await Infrastructure.TailMuxTestHelpers.WaitUntilAsync(() => collected.Count >= 2, TimeSpan.FromSeconds(2));
        Assert.True(ok, "Timed out waiting for writes to be observed");
        cts.Cancel();
        await readerTask;

        Assert.Contains("alpha", collected);
        Assert.Contains("beta", collected);
    }
}
